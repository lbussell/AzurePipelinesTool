// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Spectre.Console;

namespace AzurePipelinesTool.Display;

internal sealed class InteractionService(IAnsiConsole ansiConsole)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;

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
}
