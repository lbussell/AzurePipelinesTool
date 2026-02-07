// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Markout;
using AzurePipelinesTool.AzureDevOps;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

internal sealed class ListCommand(
    IAnsiConsole ansiConsole,
    PipelinesService pipelinesService
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    /// <summary>
    /// List all pipelines associated with the current repository.
    /// </summary>
    [Command("list|ls")]
    public async Task ExecuteAsync()
    {
        var pipelines = await _pipelinesService.GetLocalPipelinesAsync().ToListAsync();

        var writer = new MarkoutWriter(_ansiConsole.Profile.Out.Writer);
        writer.WriteTableStart("Name", "ID", "Definition");

        foreach (var pipeline in pipelines)
            writer.WriteTableRow(pipeline.Name, pipeline.Id.Value.ToString(), pipeline.RelativePath);

        writer.WriteTableEnd();
        writer.Flush();
    }
}
