// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;

namespace PipelineMonitor.Commands;

internal sealed class DiscoverCommand(
    InteractionService interactionService,
    PipelinesService pipelinesService)
{
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    [Command("discover")]
    public async Task ExecuteAsync()
    {
        var pipelinesTask = _pipelinesService
            .GetLocalPipelinesAsync()
            .ToListAsync()
            .AsTask();

        IReadOnlyList<LocalPipelineInfo> pipelines = await _interactionService
            .ShowLoadingAsync("Loading pipelines...", () => pipelinesTask);

        foreach (var pipeline in pipelines)
        {
            Console.WriteLine();
            Console.WriteLine($"{pipeline.Name}");
            Console.WriteLine($"File: {pipeline.RelativePath}");
            Console.WriteLine($"ID: {pipeline.Id.Value}");
        }
    }
}
