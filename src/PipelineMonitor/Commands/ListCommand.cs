// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class ListCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelinesService pipelinesService
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    [Command("list|ls")]
    public async Task ExecuteAsync()
    {
        var pipelinesTask = _pipelinesService.GetLocalPipelinesAsync().ToListAsync().AsTask();

        IReadOnlyList<LocalPipelineInfo> pipelines = await _interactionService.ShowLoadingAsync(
            "Loading pipelines...",
            () => pipelinesTask
        );

        foreach (var pipeline in pipelines)
        {
            _ansiConsole.WriteLine();
            _ansiConsole.Display(pipeline);
        }
    }
}
