// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Spectre.Console;

namespace PipelineMonitor;

internal sealed class InteractionService(IAnsiConsole ansiConsole)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;

    public bool IsInteractive { get; } = !Console.IsInputRedirected
        && !Console.IsOutputRedirected
        && ansiConsole.Profile.Capabilities.Interactive;

    public async Task<T> ShowLoadingAsync<T>(string statusText, Func<Task<T>> action)
    {
        Console.Write(statusText);
        try
        {
            var result = await action();
            Console.WriteLine(" Done.");
            return result;
        }
        catch
        {
            Console.WriteLine();
            throw;
        }
    }

    public void DisplaySubtleMessage(string message, bool escapeMarkup = true)
    {
        var displayMessage = escapeMarkup ? message.EscapeMarkup() : message;
        _ansiConsole.MarkupLine($"[dim]{displayMessage}[/]");
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

