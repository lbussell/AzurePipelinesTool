// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps.Yaml;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PipelineMonitor.Commands;

internal sealed class ParametersCommand(
    IAnsiConsole ansiConsole,
    IInteractionService interactionService,
    IPipelineResolver pipelineResolver,
    IPipelineYamlService pipelineYamlService)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly IInteractionService _interactionService = interactionService;
    private readonly IPipelineResolver _pipelineResolver = pipelineResolver;
    private readonly IPipelineYamlService _pipelineYamlService = pipelineYamlService;

    [Command("parameters")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        var parseTask = _pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName);
        var pipelineYaml = await _interactionService.ShowStatusAsync("Parsing YAML...", () => parseTask);
        if (pipelineYaml is null)
            throw new UserFacingException("Failed to parse pipeline YAML file.");

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
}
