// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Collections;
using Markout;
using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.AzureDevOps.Yaml;

namespace AzurePipelinesTool.Display;

/// <summary>
/// View model for the <c>info</c> command. Renders pipeline details with optional Variables and Parameters sections.
/// </summary>
[MarkoutSerializable(TitleProperty = nameof(Name), AutoFields = false)]
public sealed class PipelineInfoView
{
    [MarkoutIgnore]
    public string Name { get; init; } = "";

    [MarkoutIgnore]
    public int Id { get; init; }

    [MarkoutIgnore]
    public string RelativePath { get; init; } = "";

    [MarkoutIgnore]
    public string Organization { get; init; } = "";

    [MarkoutIgnore]
    public string Project { get; init; } = "";

    [MarkoutIgnore]
    public string Repository { get; init; } = "";

    [MarkoutIgnoreInTable]
    public List<MarkoutField> Info =>
    [
        MarkoutField.Create("ID", Id),
        MarkoutField.Create("Definition", RelativePath),
    ];

    [MarkoutIgnoreInTable]
    public List<MarkoutField> Context =>
    [
        MarkoutField.Create("Organization", Organization),
        MarkoutField.Create("Project", Project),
        MarkoutField.Create("Repository", Repository),
    ];

    [MarkoutSection(Name = "Variables")]
    public List<VariableRowView>? Variables { get; init; }

    [MarkoutSection(Name = "Parameters")]
    public List<ParameterRowView>? Parameters { get; init; }

    internal static PipelineInfoView From(
        LocalPipelineInfo pipeline,
        IReadOnlyList<PipelineVariableInfo>? variables,
        IReadOnlyList<PipelineParameter>? parameters) => new()
    {
        Name = pipeline.Name,
        Id = pipeline.Id.Value,
        RelativePath = pipeline.RelativePath,
        Organization = pipeline.Organization.Name,
        Project = pipeline.Project.Name,
        Repository = pipeline.Repository.Name,
        Variables = variables is { Count: > 0 }
            ? variables.Select(VariableRowView.From).ToList()
            : null,
        Parameters = parameters is { Count: > 0 }
            ? parameters.Select(ParameterRowView.From).ToList()
            : null,
    };
}

/// <summary>
/// Table row for pipeline variable display.
/// </summary>
[MarkoutSerializable]
public sealed class VariableRowView
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";

    [MarkoutBoolFormat("yes", "no")]
    public bool Secret { get; init; }

    [MarkoutBoolFormat("yes", "no")]
    public bool Settable { get; init; }

    internal static VariableRowView From(PipelineVariableInfo v) => new()
    {
        Name = v.Name,
        Value = v.IsSecret ? "***" : v.Value,
        Secret = v.IsSecret,
        Settable = v.AllowOverride,
    };
}

/// <summary>
/// Table row for pipeline parameter display.
/// </summary>
[MarkoutSerializable]
public sealed class ParameterRowView
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";

    [MarkoutSkipDefault]
    public string? Default { get; init; }

    [MarkoutPropertyName("Display Name")]
    [MarkoutSkipDefault]
    public string? DisplayName { get; init; }

    [MarkoutSkipDefault]
    public string? Values { get; init; }

    internal static ParameterRowView From(PipelineParameter p) => new()
    {
        Name = p.Name,
        Type = p.Type,
        Default = p.Default is not null ? FormatValue(p.Default) : null,
        DisplayName = p.DisplayName,
        Values = p.Values is { Count: > 0 } ? string.Join(", ", p.Values) : null,
    };

    private static string FormatValue(object value) =>
        value switch
        {
            IDictionary dict => "{" + string.Join(", ", dict.Keys.Cast<object>().Select(k => $"{k}: {dict[k]}")) + "}",
            IList list => "[" + string.Join(", ", list.Cast<object>()) + "]",
            _ => value.ToString() ?? "",
        };
}
