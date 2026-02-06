// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class InfoCommand(IAnsiConsole ansiConsole, PipelineResolver pipelineResolver)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;

    [Command("info")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);
        _ansiConsole.Display(pipeline);
    }
}
