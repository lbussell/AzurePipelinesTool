// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;

namespace PipelineMonitor.Commands;

internal sealed class ParametersCommand(
    InteractionService interactionService,
    PipelineResolver pipelineResolver,
    PipelineYamlService pipelineYamlService)
{
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelineYamlService _pipelineYamlService = pipelineYamlService;

    [Command("parameters")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var parseTask = _pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName);
        var pipelineYaml = await _interactionService.ShowLoadingAsync("Parsing YAML...", () => parseTask);
        if (pipelineYaml is null)
            throw new UserFacingException("Failed to parse pipeline YAML file.");

        if (pipelineYaml.Parameters.Count == 0)
        {
            _interactionService.DisplayWarning("No parameters defined in this pipeline.");
            return;
        }

        Console.WriteLine("Parameters:");
        Console.WriteLine();

        foreach (var param in pipelineYaml.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(param.DisplayName))
                Console.WriteLine($"  {param.DisplayName.Trim()}");

            var defaultText = "";
            if (param.ParameterType is PipelineParameterType.StringList
                && param.Default is IEnumerable<object> defaults)
            {
                defaultText = string.Join(", ", defaults.Select(d => d.ToString() ?? "unknown"));
            }
            else if (param.Default is not null)
            {
                defaultText = param.Default.ToString() ?? "";
            }

            Console.WriteLine($"  {param.Name}: {defaultText}");
            Console.WriteLine();
        }
    }
}
