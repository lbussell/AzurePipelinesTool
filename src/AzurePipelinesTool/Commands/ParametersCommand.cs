// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Markout;
using AzurePipelinesTool.AzureDevOps.Yaml;
using AzurePipelinesTool.Display;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

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

        var rows = pipelineYaml.Parameters.Select(ParameterRowView.From).ToList();

        var writer = new MarkoutWriter(_ansiConsole.Profile.Out.Writer);
        writer.WriteTableStart("Name", "Type", "Default", "Display Name", "Values");

        foreach (var row in rows)
            writer.WriteTableRow(row.Name, row.Type, row.Default ?? "", row.DisplayName ?? "", row.Values ?? "");

        writer.WriteTableEnd();
        writer.Flush();
    }
}
