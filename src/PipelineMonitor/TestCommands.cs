// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

namespace PipelineMonitor;

internal sealed class TestCommands(IInteractionService interactionService)
{
    private readonly IInteractionService _interactionService = interactionService;

    [Command("hello")]
    public async Task GreetUserAsync(string? name = null)
    {
        name ??= await _interactionService.PromptAsync<string>("What is your name?");
        Console.WriteLine($"Hello, {name}!");
    }

    [Command("confirm")]
    public async Task ConfirmActionAsync()
    {
        var ok = await _interactionService.ConfirmAsync("All good?");
        Console.WriteLine(ok ? "OK!" : "Not OK!");
    }
}
