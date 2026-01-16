// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PipelineMonitor;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.Logging;

using Spectre.Console;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

builder.Services.TryAddPipelinesService();
builder.Services.TryAddInteractionService();

builder.Logging.ClearProviders();
builder.Logging.AddFileLogger(builder.Configuration);
builder.Logging.AddLogLocationOnExit();

var consoleAppBuilder = builder.ToConsoleAppBuilder();
consoleAppBuilder.Add<App>();

var runTask = consoleAppBuilder.RunAsync(args);
await runTask;

internal sealed class App(
    IAnsiConsole ansiConsole,
    IInteractionService interactionService)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IInteractionService _interactionService = interactionService;

    [Command("discover")]
    public async Task DiscoverPipelinesAsync([FromServices] PipelinesService pipelinesService)
    {
        var pipelinesTask = pipelinesService
            .GetLocalPipelinesAsync()
            .ToListAsync()
            .AsTask();

        IReadOnlyList<LocalPipelineInfo> pipelines = await _interactionService
            .ShowStatusAsync("Loading pipelines...", () => pipelinesTask);

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Definition")
            .AddColumn("Pipeline");

        foreach (var pipeline in pipelines)
            table.AddRow(
                $"[blue]{pipeline.RelativePath}[/]",
                $"[bold green]{pipeline.Name}[/]");

        _ansiConsole.Write(table);
    }

    [Command("info")]
    public async Task ShowPipelineInfoAsync(
        [FromServices] PipelinesService pipelinesService,
        [Argument] string definitionPath)
    {
        var pipelineFile = new FileInfo(definitionPath);
        if (!pipelineFile.Exists)
        {
            _ansiConsole.MarkupLine($"[red]Error:[/] Definition file '{definitionPath}' does not exist.");
            return;
        }

        var pipelinesTask = pipelinesService
            .GetLocalPipelinesAsync()
            .ToListAsync()
            .AsTask();

        List<LocalPipelineInfo> pipelines = await _interactionService
            .ShowStatusAsync("Loading...", () => pipelinesTask);

        var thisPipeline = pipelines.FirstOrDefault(pipeline =>
            pipeline.DefinitionFile.FullName.Equals(pipelineFile.FullName));

        if (thisPipeline is null)
        {
            _ansiConsole.MarkupLine($"[red]Error:[/] No pipeline found for definition file '{definitionPath}'.");
            return;
        }

        _ansiConsole.MarkupLine($"[blue]{thisPipeline.RelativePath}[/] refers to pipeline [bold green]{thisPipeline.Name}[/] [dim](ID: {thisPipeline.Id.Value})[/]");
    }
}
