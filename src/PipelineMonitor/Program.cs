// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PipelineMonitor;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;
using PipelineMonitor.Logging;

using Spectre.Console;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

builder.Services.TryAddPipelinesService();
builder.Services.TryAddInteractionService();
builder.Services.TryAddPipelineYamlService();

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

    [Command("parameters")]
    public async Task ShowParametersAsync(
        [FromServices] IPipelineYamlService pipelineYamlService,
        [Argument] string definitionPath)
    {
        var pipelineFile = new FileInfo(definitionPath);
        if (!pipelineFile.Exists)
        {
            _ansiConsole.MarkupLine($"[red]Error:[/] Definition file '{definitionPath}' does not exist.");
            return;
        }

        var parseTask = pipelineYamlService.ParseAsync(pipelineFile.FullName);
        var pipeline = await _interactionService.ShowStatusAsync("Parsing YAML...", () => parseTask);

        if (pipeline is null)
        {
            _ansiConsole.MarkupLine("[red]Error:[/] Failed to parse pipeline YAML file.");
            return;
        }

        if (pipeline.Parameters is null || pipeline.Parameters.Count == 0)
        {
            _ansiConsole.MarkupLine("[yellow]No parameters defined in this pipeline.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Default")
            .AddColumn("Values");

        foreach (var param in pipeline.Parameters)
        {
            var defaultValue = param.Default?.ToString() ?? "[dim]<none>[/]";
            var values = param.Values is not null
                ? string.Join(", ", param.Values)
                : "[dim]<any>[/]";

            table.AddRow(
                $"[bold]{param.Name}[/]",
                $"[blue]{param.Type}[/]",
                defaultValue,
                values);
        }

        _ansiConsole.Write(table);
    }
}
