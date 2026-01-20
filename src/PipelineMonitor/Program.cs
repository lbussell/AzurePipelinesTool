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
using Spectre.Console.Rendering;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

builder.Services.TryAddPipelinesService();
builder.Services.TryAddInteractionService();
builder.Services.TryAddPipelineYamlService();

builder.Logging.ClearProviders();
builder.Logging.AddFileLogger(builder.Configuration);

#if DEBUG
builder.Logging.AddLogLocationOnExit();
#endif

var consoleAppBuilder = builder.ToConsoleAppBuilder();
consoleAppBuilder.Add<App>();

#if DEBUG
consoleAppBuilder.Add<TestCommands>();
#endif

var runTask = consoleAppBuilder.RunAsync(args);
await runTask;

internal sealed class App(
    IAnsiConsole ansiConsole,
    IInteractionService interactionService,
    PipelinesService pipelinesService)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IInteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;

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
        _ansiConsole.WriteLine();
    }

    [Command("info")]
    public async Task ShowPipelineInfoAsync([Argument] string definitionPath)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;
        _ansiConsole.Write(pipeline.SingleLineDisplay);
        _ansiConsole.WriteLine();
    }

    [Command("parameters")]
    public async Task ShowParametersAsync(
        [FromServices] IPipelineYamlService pipelineYamlService,
        [Argument] string definitionPath)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;

        var parseTask = pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName);
        var pipelineYaml = await _interactionService.ShowStatusAsync("Parsing YAML...", () => parseTask);
        if (pipelineYaml is null)
        {
            _interactionService.DisplayError("Failed to parse pipeline YAML file.");
            return;
        }

        if (pipelineYaml.Parameters.Count == 0)
        {
            _interactionService.DisplayWarning("No parameters defined in this pipeline.");
            return;
        }

        IRenderable title = new Markup("[bold]Parameters[/]").PadVertical();
        List<IRenderable> content = [title];

        foreach (var param in pipelineYaml.Parameters)
        {
            List<IRenderable> paramContent = [];

            if (!string.IsNullOrWhiteSpace(param.DisplayName))
                paramContent.Add(new Markup(param.DisplayName.Trim().EscapeMarkup()));

            string defaultText = "";
            if (param.ParameterType is PipelineParameterType.StringList
                && param.Default is IEnumerable<object> defaults)
            {
                defaultText = string.Join(", ", defaults.Select(d => d.ToString() ?? "unknown")).EscapeMarkup();
            }
            else if (param.Default is not null)
            {
                defaultText = param.Default.ToString().EscapeMarkup();
            }

            paramContent.Add(new Markup($"[blue]{param.Name.EscapeMarkup()}[/]: [dim]{defaultText}[/]"));

            content.Add(new Rows(paramContent).PadBottom());
        }

        var display = new Rows(content);
        _ansiConsole.Write(display);
        _ansiConsole.WriteLine();
    }

    [Command("variables")]
    public async Task ShowVariablesAsync([Argument] string definitionPath)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;

        var variablesTask = _pipelinesService.GetVariablesAsync(pipeline);
        var variables = await _interactionService.ShowStatusAsync("Loading variables...", () => variablesTask);

        if (variables.Count == 0)
        {
            _interactionService.DisplayWarning("No variables defined in this pipeline.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Name")
            .AddColumn("Value")
            .AddColumn("Secret")
            .AddColumn("Allow Override");

        foreach (var variable in variables)
        {
            var valueDisplay = variable.IsSecret ? "[dim]***[/]" : (variable.Value ?? string.Empty).EscapeMarkup();
            var secretDisplay = variable.IsSecret ? "[yellow]Yes[/]" : "No";
            var allowOverrideDisplay = variable.AllowOverride ? "Yes" : "No";

            table.AddRow(
                $"[blue]{variable.Name.EscapeMarkup()}[/]",
                valueDisplay,
                secretDisplay,
                allowOverrideDisplay);
        }

        _ansiConsole.Write(table);
        _ansiConsole.WriteLine();
    }

    [Command("variables export")]
    public async Task ExportVariablesAsync(
        [Argument] string definitionPath,
        [Argument] string outputFile)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;

        var variablesTask = _pipelinesService.GetVariablesAsync(pipeline);
        var variables = await _interactionService.ShowStatusAsync("Loading variables...", () => variablesTask);

        var json = System.Text.Json.JsonSerializer.Serialize(
            variables,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(outputFile, json);
        _interactionService.DisplaySuccess($"Exported {variables.Count} variable(s) to {outputFile}");
    }

    [Command("variables import")]
    public async Task ImportVariablesAsync(
        [Argument] string definitionPath,
        [Argument] string inputFile,
        bool clear = false)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;

        if (!File.Exists(inputFile))
        {
            _interactionService.DisplayError($"Input file '{inputFile}' does not exist.");
            return;
        }

        var json = await File.ReadAllTextAsync(inputFile);
        var variables = System.Text.Json.JsonSerializer.Deserialize<List<PipelineVariableInfo>>(json);

        if (variables is null || variables.Count == 0)
        {
            _interactionService.DisplayWarning("No variables found in the input file.");
            return;
        }

        await _interactionService.ShowStatusAsync("Setting variables...", async () =>
        {
            await _pipelinesService.SetVariablesAsync(pipeline, variables, clear);
            return true;
        });

        var actionDescription = clear ? "replaced with" : "imported";
        _interactionService.DisplaySuccess($"Successfully {actionDescription} {variables.Count} variable(s).");
    }

    [Command("runs")]
    public async Task ShowRunsAsync(
        [Argument] string definitionPath,
        int top = 10)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;

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

    private async Task<IEnumerable<LocalPipelineInfo>> GetLocalPipelinesAsync()
    {
        var pipelinesTask = _pipelinesService
            .GetLocalPipelinesAsync()
            .ToListAsync()
            .AsTask();

        List<LocalPipelineInfo> pipelines = await _interactionService
            .ShowStatusAsync("Loading Pipelines...", () => pipelinesTask);

        return pipelines;
    }

    private async Task<LocalPipelineInfo?> GetLocalPipelineAsync(string definitionPath)
    {
        var pipelines = await GetLocalPipelinesAsync();
        var pipelineFile = new FileInfo(definitionPath);

        if (!pipelineFile.Exists)
        {
            _interactionService.DisplayError($"Definition file '{definitionPath}' does not exist.");
            return null;
        }

        var matchingPipelines = pipelines
            .Where(pipeline =>
                pipeline.DefinitionFile.FullName.Equals(pipelineFile.FullName))
            .ToList();

        var pipelineInfo = matchingPipelines.FirstOrDefault();

        if (matchingPipelines.Count > 1)
            // Prompt user to select which pipeline they meant
            pipelineInfo = await _interactionService.SelectAsync(
                "Multiple pipelines found for the specified definition file. Please select one:",
                matchingPipelines,
                pipeline => pipeline.Name);

        if (pipelineInfo is null)
            _interactionService.DisplayError($"No pipeline found for definition file '{definitionPath}'.");

        return pipelineInfo;
    }
}
