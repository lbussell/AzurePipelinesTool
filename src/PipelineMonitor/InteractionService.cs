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
    Task<T> ShowStatusAsync<T>(string statusText, Func<Task<T>> action);

    void DisplaySubtleMessage(string message, bool escapeMarkup = true);

    void DisplayError(string message, bool escapeMarkup = true);

    void DisplayWarning(string message, bool escapeMarkup = true);
}

internal sealed class InteractionService(
    IAnsiConsole ansiConsole,
    IHostApplicationLifetime applicationLifetime) : IInteractionService
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;

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

    private void DisplaySpecialMessage(string messageType, string color, string message, bool escapeMarkup)
    {
        var displayMessage = escapeMarkup ? message.EscapeMarkup() : message;
        _ansiConsole.MarkupLine($"[{color}][[{messageType}]][/] {displayMessage}");
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
