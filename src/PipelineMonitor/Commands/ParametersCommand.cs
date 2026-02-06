// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class ParametersCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelineResolver pipelineResolver,
    PipelineYamlService pipelineYamlService
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelineYamlService _pipelineYamlService = pipelineYamlService;

    [Command("parameters")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var pipelineYaml =
            await _pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName)
            ?? throw new UserFacingException("Failed to parse pipeline YAML file.");

        if (pipelineYaml.Parameters.Count == 0)
        {
            _interactionService.DisplayWarning("No parameters defined in this pipeline.");
            return;
        }

        _ansiConsole.WriteLine("Parameters:");
        _ansiConsole.WriteLine();

        foreach (var param in pipelineYaml.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(param.DisplayName))
                _ansiConsole.WriteLine($"  {param.DisplayName.Trim()}");

            var defaultText = "";
            if (
                param.ParameterType is PipelineParameterType.StringList
                && param.Default is IEnumerable<object> defaults
            )
            {
                defaultText = string.Join(", ", defaults.Select(d => d.ToString() ?? "unknown"));
            }
            else if (param.Default is not null)
            {
                defaultText = param.Default.ToString() ?? "";
            }

            _ansiConsole.WriteLine($"  {param.Name}: {defaultText}");
            _ansiConsole.WriteLine();
        }
    }
}
