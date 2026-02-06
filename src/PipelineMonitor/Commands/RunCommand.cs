// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;
using PipelineMonitor.Git;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class RunCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelineResolver pipelineResolver,
    PipelineYamlService pipelineYamlService,
    PipelinesService pipelinesService,
    GitService gitService,
    IEnvironment environment
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;
    private readonly PipelineYamlService _pipelineYamlService = pipelineYamlService;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly GitService _gitService = gitService;
    private readonly IEnvironment _environment = environment;

    /// <summary>
    /// Preview-expand a pipeline's YAML by calling the Azure DevOps Preview API.
    /// </summary>
    /// <param name="definitionPath">Relative path to the pipeline YAML file.</param>
    /// <param name="parameter">-p, Template parameters as key=value pairs.</param>
    [Command("check")]
    public async Task ExecuteAsync([Argument] string definitionPath, string[]? parameter = null)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        await EnsureGitInSyncAsync();

        var templateParameters = ParseKeyValuePairs(parameter ?? [], "parameter");

        await ValidateParametersAsync(pipeline, templateParameters);

        var branch = await _gitService.GetCurrentBranchAsync();
        var refName = branch is not null ? $"refs/heads/{branch}" : null;

        _ansiConsole.WriteLine($"Checking pipeline '{pipeline.Name}'...");

        string finalYaml;
        try
        {
            finalYaml = await _pipelinesService.PreviewPipelineAsync(pipeline, refName, templateParameters);
        }
        catch (Exception ex)
        {
            throw new UserFacingException($"Pipeline preview failed: {ex.Message}", ex);
        }

        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"pipeline-check-{pipeline.Id.Value}-{DateTime.Now:yyyyMMdd-HHmmss}.yml"
        );
        await File.WriteAllTextAsync(tempFile, finalYaml);

        _interactionService.DisplaySuccess($"Pipeline YAML expanded successfully.");
        _ansiConsole.WriteLine($"Output: {tempFile}");
    }

    /// <summary>
    /// Queue a pipeline run on Azure DevOps.
    /// </summary>
    /// <param name="definitionPath">Relative path to the pipeline YAML file.</param>
    /// <param name="parameter">-p, Template parameters as key=value pairs.</param>
    /// <param name="variable">--var, Pipeline variable overrides as key=value pairs.</param>
    /// <param name="skipStage">-s|--skip, Stage names to skip.</param>
    [Command("run")]
    public async Task RunAsync(
        [Argument] string definitionPath,
        string[]? parameter = null,
        string[]? variable = null,
        string[]? skipStage = null
    )
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);

        await EnsureGitInSyncAsync();

        var templateParameters = ParseKeyValuePairs(parameter ?? [], "parameter");
        var variables = ParseKeyValuePairs(variable ?? [], "variable");

        await ValidateParametersAsync(pipeline, templateParameters);
        await ValidateVariablesAsync(pipeline, variables);

        var branch = await _gitService.GetCurrentBranchAsync();
        var refName = branch is not null ? $"refs/heads/{branch}" : null;

        _ansiConsole.WriteLine($"Queuing pipeline '{pipeline.Name}'...");

        QueuedPipelineRunInfo runInfo;
        try
        {
            runInfo = await _pipelinesService.RunPipelineAsync(pipeline, refName, templateParameters, variables, skipStage);
        }
        catch (Exception ex)
        {
            throw new UserFacingException($"Failed to queue pipeline run: {ex.Message}", ex);
        }

        _interactionService.DisplaySuccess($"Pipeline run queued successfully.");
        _ansiConsole.MarkupLineInterpolated($"Run: [link={runInfo.WebUrl}]{runInfo.WebUrl}[/]");
        var exe = Path.GetFileNameWithoutExtension(_environment.ProcessPath) ?? "pipelinemon";
        _interactionService.DisplaySubtleMessage($"To cancel: {exe} cancel {runInfo.Id.Value}");
    }

    private async Task EnsureGitInSyncAsync()
    {
        var workingTree = await _gitService.GetWorkingTreeStatusAsync();
        if (!workingTree.IsClean)
            throw new UserFacingException(
                "Working tree has uncommitted changes. Commit or stash changes before running check."
            );

        var upstream = await _gitService.GetUpstreamBranchAsync();
        if (upstream is null)
            throw new UserFacingException(
                "No upstream branch configured. Set an upstream branch before running check."
            );

        var aheadBehind = await _gitService.GetAheadBehindAsync();
        if (aheadBehind is null)
            throw new UserFacingException("Could not determine sync status with upstream.");

        var (ahead, behind) = aheadBehind.Value;
        if (ahead > 0 || behind > 0)
            throw new UserFacingException(
                $"Branch is {ahead} ahead and {behind} behind upstream. Push/pull to sync before running check."
            );
    }

    private async Task ValidateVariablesAsync(LocalPipelineInfo pipeline, Dictionary<string, string> variables)
    {
        if (variables.Count == 0)
            return;

        var definedVariables = await _pipelinesService.GetVariablesAsync(pipeline);
        var definedNames = definedVariables.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);

        var unknownVars = variables.Keys.Where(k => !definedNames.ContainsKey(k)).ToList();

        if (unknownVars.Count > 0)
        {
            var defined =
                definedVariables.Count > 0 ? string.Join(", ", definedVariables.Select(v => v.Name)) : "(none)";
            throw new UserFacingException(
                $"Unknown variable(s): {string.Join(", ", unknownVars)}. Defined variables: {defined}"
            );
        }

        var nonOverridableVars = variables
            .Keys.Where(k => definedNames.TryGetValue(k, out var v) && !v.AllowOverride)
            .ToList();

        if (nonOverridableVars.Count > 0)
            throw new UserFacingException(
                $"Variable(s) not allowed to override: {string.Join(", ", nonOverridableVars)}. Set 'Settable at queue time' in the pipeline definition."
            );
    }

    private async Task ValidateParametersAsync(
        LocalPipelineInfo pipeline,
        Dictionary<string, string> templateParameters
    )
    {
        if (templateParameters.Count == 0)
            return;

        var pipelineYaml = await _pipelineYamlService.ParseAsync(pipeline.DefinitionFile.FullName);
        if (pipelineYaml is null)
            throw new UserFacingException("Failed to parse pipeline YAML file for parameter validation.");

        var definedNames = pipelineYaml.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknownParams = templateParameters.Keys.Where(k => !definedNames.Contains(k)).ToList();

        if (unknownParams.Count > 0)
        {
            var defined =
                pipelineYaml.Parameters.Count > 0
                    ? string.Join(", ", pipelineYaml.Parameters.Select(p => p.Name))
                    : "(none)";
            throw new UserFacingException(
                $"Unknown parameter(s): {string.Join(", ", unknownParams)}. Defined parameters: {defined}"
            );
        }
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string[] pairs, string label)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            var equalsIndex = pair.IndexOf('=');
            if (equalsIndex <= 0)
                throw new UserFacingException($"Invalid {label} format: '{pair}'. Expected 'key=value'.");

            var key = pair[..equalsIndex];
            var value = pair[(equalsIndex + 1)..];
            result[key] = value;
        }

        return result;
    }
}
