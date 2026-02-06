// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.Json;
using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;

namespace PipelineMonitor.Commands;

internal sealed class VariablesCommand(
    InteractionService interactionService,
    PipelineResolver pipelineResolver,
    PipelinesService pipelinesService)
{
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
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
        var variables = await _interactionService.ShowLoadingAsync("Loading variables...", () => variablesTask);

        if (variables.Count == 0)
        {
            _interactionService.DisplayWarning("No variables defined in this pipeline.");
            return;
        }

        foreach (var variable in variables)
        {
            var value = variable.IsSecret ? "***" : (variable.Value ?? "");
            var flags = string.Join(", ",
                new[] { variable.IsSecret ? "secret" : null, variable.AllowOverride ? "allow override" : null }
                    .Where(f => f is not null));
            var flagsDisplay = flags.Length > 0 ? $" ({flags})" : "";
            Console.WriteLine($"  {variable.Name}: {value}{flagsDisplay}");
        }
    }

    [Command("variables export")]
    public async Task ExportAsync(
        [Argument] string definitionPath,
        [Argument] string outputFile)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var variablesTask = _pipelinesService.GetVariablesAsync(pipeline);
        var variables = await _interactionService.ShowLoadingAsync("Loading variables...", () => variablesTask);

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

        await _interactionService.ShowLoadingAsync("Setting variables...", async () =>
        {
            await _pipelinesService.SetVariablesAsync(pipeline, variables, clear);
            return true;
        });

        var actionDescription = clear ? "replaced with" : "imported";
        _interactionService.DisplaySuccess($"Successfully {actionDescription} {variables.Count} variable(s).");
    }
}
