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

        var variablesTask = _pipelinesService.GetVariablesForLocalPipelineAsync(pipeline.Id);
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

    [Command("runs")]
    public async Task ShowRunsAsync(
        [Argument] string definitionPath,
        int top = 10)
    {
        var pipeline = await GetLocalPipelineAsync(definitionPath);
        if (pipeline is null) return;

        var runsTask = _pipelinesService
            .GetRunsForLocalPipelineAsync(pipeline.Id, top)
            .ToListAsync()
            .AsTask();

        IReadOnlyList<PipelineRunInfo> runs = await _interactionService
            .ShowStatusAsync("Loading pipeline runs...", () => runsTask);

        if (runs.Count == 0)
        {
            _interactionService.DisplaySubtleMessage("No runs found for this pipeline.");
            return;
        }

        IEnumerable<IRenderable> content =
        [
            new Markup($"Recent Runs for [bold green]{pipeline.Name}[/]:").PadBottom(),
            runs.ToTable(),
        ];

        IRenderable display = new Rows(content);
        display = new Padder(display);

        _ansiConsole.Write(display);
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

        var thisPipeline = pipelines.FirstOrDefault(pipeline =>
            pipeline.DefinitionFile.FullName.Equals(pipelineFile.FullName));

        if (thisPipeline is null)
        {
            _interactionService.DisplayError($"No pipeline found for definition file '{definitionPath}'.");
            return null;
        }

        return thisPipeline;
    }
}
