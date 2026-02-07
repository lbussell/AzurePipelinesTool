// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.Json;
using ConsoleAppFramework;
using Markout;
using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.Display;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

internal sealed class VariablesCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelineResolver pipelineResolver,
    PipelinesService pipelinesService
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelinesService _pipelinesService = pipelinesService;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Command("variables")]
    public async Task ShowAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);
        var variables = await _pipelinesService.GetVariablesAsync(pipeline);

        if (variables.Count == 0)
        {
            _interactionService.DisplayWarning("No variables defined in this pipeline.");
            return;
        }

        var rows = variables.Select(VariableRowView.From).ToList();

        var writer = new MarkoutWriter(_ansiConsole.Profile.Out.Writer);
        writer.WriteTableStart("Name", "Value", "Secret", "Settable");
        foreach (var row in rows)
            writer.WriteTableRow(row.Name, row.Value, row.Secret ? "yes" : "no", row.Settable ? "yes" : "no");
        writer.WriteTableEnd();
        writer.Flush();
    }

    [Command("variables export")]
    public async Task ExportAsync([Argument] string definitionPath, [Argument] string outputFile)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);
        var variables = await _pipelinesService.GetVariablesAsync(pipeline);
        var json = JsonSerializer.Serialize(variables, JsonOptions);
        await File.WriteAllTextAsync(outputFile, json);
        _interactionService.DisplaySuccess($"Exported {variables.Count} variable(s) to {outputFile}");
    }

    [Command("variables import")]
    public async Task ImportAsync([Argument] string definitionPath, [Argument] string inputFile, bool clear = false)
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

        await _pipelinesService.SetVariablesAsync(pipeline, variables, clear);

        var actionDescription = clear ? "replaced with" : "imported";
        _interactionService.DisplaySuccess($"Successfully {actionDescription} {variables.Count} variable(s).");
    }
}
