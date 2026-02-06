// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class RunsCommand(
    IAnsiConsole ansiConsole,
    PipelineResolver pipelineResolver,
    PipelinesService pipelinesService
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    [Command("runs")]
    public async Task ExecuteAsync([Argument] string definitionPath, int top = 10)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);
        var pipelineRuns = _pipelinesService.GetRunsAsync(pipeline, top);

        await foreach (var run in pipelineRuns)
        {
            var resultSymbol = run.Result switch
            {
                PipelineRunResult.Succeeded => "✓",
                PipelineRunResult.PartiallySucceeded => "~",
                PipelineRunResult.Failed => "✗",
                PipelineRunResult.Canceled => "/",
                _ => "-",
            };

            var commitMessage = run.Commit?.Message ?? "";
            _ansiConsole.WriteLine($"[{resultSymbol}] {run.Name} - {commitMessage} ({run.State})");
        }
    }
}
