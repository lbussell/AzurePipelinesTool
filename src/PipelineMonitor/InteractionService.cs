// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PipelineMonitor;

internal interface IInteractionService
{
    bool IsInteractive { get; }

    Task<T> ShowStatusAsync<T>(string statusText, Func<Task<T>> action);

    void DisplaySubtleMessage(string message, bool escapeMarkup = true);

    void DisplayError(string message, bool escapeMarkup = true);

    void DisplayWarning(string message, bool escapeMarkup = true);

    void DisplaySuccess(string message, bool escapeMarkup = true);

    Task<T> PromptAsync<T>(string prompt, T? defaultValue = default) where T : notnull;

    Task<T> SelectAsync<T>(string prompt, IEnumerable<T> choices,
        Func<T, string>? displaySelector = null, T? defaultValue = default) where T : notnull;

    Task<IReadOnlyList<T>> MultiSelectAsync<T>(string prompt, IEnumerable<T> choices,
        Func<T, string>? displaySelector = null, IEnumerable<T>? defaults = null,
        bool required = false) where T : notnull;

    Task<bool> ConfirmAsync(string prompt, bool defaultValue = false);

    Task<string> SelectAsync(string prompt, IEnumerable<string> suggestions);
}

internal sealed class InteractionService(IAnsiConsole ansiConsole) : IInteractionService
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;

    public bool IsInteractive { get; } = !Console.IsInputRedirected
        && !Console.IsOutputRedirected
        && ansiConsole.Profile.Capabilities.Interactive;

    public async Task<T> ShowStatusAsync<T>(string statusText, Func<Task<T>> action)
    {
        // TODO: avoid spinners in non-interactive environments
        return await _ansiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .StartAsync(statusText, (context) => action());
    }

    public void DisplaySubtleMessage(string message, bool escapeMarkup = true)
    {
        var displayMessage = escapeMarkup ? message.EscapeMarkup() : message;
        IRenderable text = new Markup($"[dim]{displayMessage}[/]");
        _ansiConsole.Write(text);
        _ansiConsole.WriteLine();
    }

    public void DisplayError(string message, bool escapeMarkup = true) =>
        DisplaySpecialMessage("Error", "red", message, escapeMarkup);

    public void DisplayWarning(string message, bool escapeMarkup = true) =>
        DisplaySpecialMessage("Warning", "yellow", message, escapeMarkup);

    public void DisplaySuccess(string message, bool escapeMarkup = true) =>
        DisplaySpecialMessage("Success", "green", message, escapeMarkup);

    private void DisplaySpecialMessage(string messageType, string color, string message, bool escapeMarkup)
    {
        var displayMessage = escapeMarkup ? message.EscapeMarkup() : message;
        _ansiConsole.MarkupLine($"[{color}][[{messageType}]][/] {displayMessage}");
    }

    public async Task<T> PromptAsync<T>(string prompt, T? defaultValue = default) where T : notnull
    {
        if (!IsInteractive)
        {
            if (defaultValue is not null) return defaultValue;
            throw new InvalidOperationException($"Cannot prompt in non-interactive environment: {prompt}");
        }

        var textPrompt = new TextPrompt<T>(prompt);
        if (defaultValue is not null) textPrompt.DefaultValue(defaultValue);

        return await Task.Run(() => _ansiConsole.Prompt(textPrompt));
    }

    public async Task<T> SelectAsync<T>(
        string prompt,
        IEnumerable<T> choices,
        Func<T, string>? displaySelector = null,
        T? defaultValue = default
    ) where T : notnull
    {
        var choicesList = choices.ToList();
        if (!IsInteractive)
        {
            if (defaultValue is not null) return defaultValue;
            if (choicesList.Count == 1) return choicesList[0];
            throw new InvalidOperationException($"Cannot prompt in non-interactive environment: {prompt}");
        }

        // Reorder so default appears first (SelectionPrompt highlights first item by default)
        if (defaultValue is not null && choicesList.Remove(defaultValue))
            choicesList.Insert(0, defaultValue);

        var selection = new SelectionPrompt<T>().Title(prompt).AddChoices(choicesList);
        if (displaySelector is not null) selection.UseConverter(displaySelector);

        return await Task.Run(() => _ansiConsole.Prompt(selection));
    }

    public async Task<IReadOnlyList<T>> MultiSelectAsync<T>(
        string prompt,
        IEnumerable<T> choices,
        Func<T, string>? displaySelector = null,
        IEnumerable<T>? defaults = null,
        bool required = false
    ) where T : notnull
    {
        if (!IsInteractive)
            throw new InvalidOperationException($"Cannot prompt in non-interactive environment: {prompt}");

        var multi = new MultiSelectionPrompt<T>().Title(prompt).AddChoices(choices);
        if (required) multi.Required();
        if (displaySelector is not null) multi.UseConverter(displaySelector);
        if (defaults is not null)
        {
            foreach (var item in defaults) multi.Select(item);
        }

        return await Task.Run(() => _ansiConsole.Prompt(multi));
    }

    public async Task<bool> ConfirmAsync(string prompt, bool defaultValue = false)
    {
        if (!IsInteractive) return defaultValue;

        var confirm = new ConfirmationPrompt(prompt) { DefaultValue = defaultValue };
        return await Task.Run(() => _ansiConsole.Prompt(confirm));
    }

    public async Task<string> SelectAsync(string prompt, IEnumerable<string> suggestions)
    {
        const string SomethingElse = "Something else...";
        List<string> choices = [.. suggestions, SomethingElse];

        var selected = await SelectAsync<string>(prompt, choices);

        if (selected == SomethingElse)
        {
            selected = await PromptAsync<string>(prompt);
        }
        else
        {
            _ansiConsole.MarkupLineInterpolated($"{prompt} [blue]{selected}[/]");
        }

        return selected;
    }
}

internal static class InteractionServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddInteractionService()
        {
            services.TryAddSingleton<IInteractionService, InteractionService>();
            services.TryAddSingleton(_ => AnsiConsole.Console);
            return services;
        }
    }
}

internal static class ConsoleRenderingExtensions
{
    extension (IRenderable renderable)
    {
        public IRenderable PadLeft(int lines = 1) => new Padder(renderable).Padding(lines, 0, 0, 0);
        public IRenderable PadTop(int lines = 1) => new Padder(renderable).Padding(0, lines, 0, 0);
        public IRenderable PadRight(int lines = 1) => new Padder(renderable).Padding(0, 0, lines, 0);
        public IRenderable PadBottom(int lines = 1) => new Padder(renderable).Padding(0, 0, 0, lines);
        public IRenderable PadVertical(int lines = 1) => new Padder(renderable).Padding(0, lines, 0, lines);
        public IRenderable PadHorizontal(int lines = 1) => new Padder(renderable).Padding(lines, 0, lines, 0);
    }
}
