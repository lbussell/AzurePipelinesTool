// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.Json;
using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class VariablesCommand(
    IAnsiConsole ansiConsole,
    IInteractionService interactionService,
    IPipelineResolver pipelineResolver,
    PipelinesService pipelinesService)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IInteractionService _interactionService = interactionService;
    private readonly IPipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [Command("variables")]
    public async Task ShowAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

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
    public async Task ExportAsync(
        [Argument] string definitionPath,
        [Argument] string outputFile)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var variablesTask = _pipelinesService.GetVariablesAsync(pipeline);
        var variables = await _interactionService.ShowStatusAsync("Loading variables...", () => variablesTask);

        var json = JsonSerializer.Serialize(variables, JsonOptions);

        await File.WriteAllTextAsync(outputFile, json);
        _interactionService.DisplaySuccess($"Exported {variables.Count} variable(s) to {outputFile}");
    }

    [Command("variables import")]
    public async Task ImportAsync(
        [Argument] string definitionPath,
        [Argument] string inputFile,
        bool clear = false)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        if (!File.Exists(inputFile))
            throw new UserFacingException($"Input file '{inputFile}' does not exist.");

        List<PipelineVariableInfo>? variables;
        try
        {
            var json = await File.ReadAllTextAsync(inputFile);
            variables = JsonSerializer.Deserialize<List<PipelineVariableInfo>>(json);
        }
        catch (JsonException ex)
        {
            throw new UserFacingException($"Failed to parse JSON file: {ex.Message}", ex);
        }

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
}
