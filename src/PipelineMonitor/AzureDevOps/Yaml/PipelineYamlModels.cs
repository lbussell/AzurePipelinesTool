// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using YamlDotNet.Serialization;

namespace PipelineMonitor.AzureDevOps.Yaml;

/// <summary>
/// Represents a pipeline parameter type in Azure Pipelines YAML.
/// </summary>
internal enum PipelineParameterType
{
    String,
    StringList,
    Number,
    Boolean,
    Object,
    Step,
    StepList,
    Job,
    JobList,
    Deployment,
    DeploymentList,
    Stage,
    StageList,
}

/// <summary>
/// Represents a single pipeline parameter definition.
/// </summary>
internal sealed record PipelineParameter
{
    [YamlMember(Alias = "name")]
    public required string Name { get; init; }

    [YamlMember(Alias = "type")]
    public string Type { get; init; } = "string";

    [YamlMember(Alias = "default")]
    public object? Default { get; init; }

    [YamlMember(Alias = "displayName")]
    public string? DisplayName { get; init; }

    [YamlMember(Alias = "values")]
    public List<string>? Values { get; init; }

    /// <summary>
    /// Gets the strongly-typed parameter type.
    /// </summary>
    public PipelineParameterType ParameterType =>
        Type?.ToLowerInvariant() switch
        {
            "string" => PipelineParameterType.String,
            "stringlist" => PipelineParameterType.StringList,
            "number" => PipelineParameterType.Number,
            "boolean" or "bool" => PipelineParameterType.Boolean,
            "object" => PipelineParameterType.Object,
            "step" => PipelineParameterType.Step,
            "steplist" => PipelineParameterType.StepList,
            "job" => PipelineParameterType.Job,
            "joblist" => PipelineParameterType.JobList,
            "deployment" => PipelineParameterType.Deployment,
            "deploymentlist" => PipelineParameterType.DeploymentList,
            "stage" => PipelineParameterType.Stage,
            "stagelist" => PipelineParameterType.StageList,
            _ => PipelineParameterType.String,
        };
}

/// <summary>
/// Represents the root of a pipeline YAML file.
/// Only includes fields relevant to parameter extraction.
/// </summary>
internal sealed record PipelineYaml
{
    [YamlMember(Alias = "parameters")]
    public List<PipelineParameter> Parameters { get; init; } = [];
}
