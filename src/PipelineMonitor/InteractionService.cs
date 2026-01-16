// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PipelineMonitor;

internal interface IInteractionService
{
    Task<T> ShowStatusAsync<T>(string statusText, Func<Task<T>> action);
    void DisplaySubtleMessage(string message, bool escapeMarkup = true);
}

internal sealed class InteractionService(IAnsiConsole ansiConsole) : IInteractionService
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;

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
