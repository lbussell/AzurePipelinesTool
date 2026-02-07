// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Markout;
using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.AzureDevOps.Yaml;
using AzurePipelinesTool.Display;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

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

    /// <summary>
    /// Show detailed information about a pipeline, including variables and parameters.
    /// </summary>
    /// <param name="definitionPath">Relative path to the pipeline YAML file.</param>
    [Command("info")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var variablesTask = _pipelinesService.GetVariablesAsync(pipeline);
        var yamlTask = _pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName);

        var variables = await variablesTask;
        var pipelineYaml = await yamlTask;

        var view = PipelineInfoView.From(pipeline, variables, pipelineYaml?.Parameters);
        MarkoutSerializer.Serialize(view, _ansiConsole.Profile.Out.Writer, AzurePipelinesToolMarkoutContext.Default);
        _ansiConsole.WriteLine();
    }
}
