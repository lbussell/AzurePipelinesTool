// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class InfoCommand(
    IAnsiConsole ansiConsole,
    IPipelineResolver pipelineResolver)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IPipelineResolver _pipelineResolver = pipelineResolver;

    [Command("info")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);
        _ansiConsole.Write(pipeline.SingleLineDisplay);
        _ansiConsole.WriteLine();
    }
}
