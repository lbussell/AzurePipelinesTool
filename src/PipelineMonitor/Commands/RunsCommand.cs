// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class RunsCommand(
    IAnsiConsole ansiConsole,
    IPipelineResolver pipelineResolver,
    PipelinesService pipelinesService)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IPipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    [Command("runs")]
    public async Task ExecuteAsync(
        [Argument] string definitionPath,
        int top = 10)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var pipelineRuns = _pipelinesService.GetRunsAsync(pipeline, top);

        var descriptionColumn = new TableColumn("Description");
        var table = new Table()
            .AddColumn("") // Result
            .AddColumn(descriptionColumn)
            .AddColumn("Stages")
            .AddColumn(new TableColumn("").RightAligned()) // Time
            .Border(TableBorder.Horizontal);

        await _ansiConsole
            .Live(table)
            .StartAsync(async context =>
            {
                await foreach (var run in pipelineRuns)
                {
                    table.AddRow(run.ResultSymbol, run.RunDetails, run.StagesSummary, run.TimeDetails);
                    context.Refresh();
                }
            });
    }
}
