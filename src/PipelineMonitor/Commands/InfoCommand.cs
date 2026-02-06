// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class InfoCommand(
    IAnsiConsole ansiConsole,
    PipelineResolver pipelineResolver,
    PipelinesService pipelinesService,
    PipelineYamlService pipelineYamlService
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly PipelineYamlService _pipelineYamlService = pipelineYamlService;

    [Command("info")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        _ansiConsole.WriteLine();
        _ansiConsole.Display(pipeline);

        var variablesTask = _pipelinesService.GetVariablesAsync(pipeline);
        var yamlTask = _pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName);

        var variables = await variablesTask;
        if (variables.Count > 0)
        {
            _ansiConsole.H2("Variables");
            _ansiConsole.DisplayVariables(variables);
        }

        var pipelineYaml = await yamlTask;
        if (pipelineYaml is not null && pipelineYaml.Parameters.Count > 0)
        {
            _ansiConsole.H2("Parameters");
            _ansiConsole.DisplayParameters(pipelineYaml.Parameters);
        }
    }
}
