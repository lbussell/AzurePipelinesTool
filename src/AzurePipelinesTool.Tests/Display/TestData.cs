// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.AzureDevOps.Yaml;

namespace AzurePipelinesTool.Tests.Display;

/// <summary>
/// Provides realistic sample data based on real command output from the
/// docker-tools-imagebuilder-unofficial pipeline (ID 1513) in the
/// docker-tools-playground repository.
/// </summary>
internal static class TestData
{
    // ── list command data ──────────────────────────────────────────────

    /// <summary>
    /// Subset of pipelines returned by <c>pipelinemon list</c> against docker-tools-playground.
    /// </summary>
    public static IReadOnlyList<LocalPipelineInfo> SamplePipelines =>
    [
        CreatePipeline("docker-tools-imagebuilder-official", 367,
            "eng/pipelines/dotnet-buildtools-image-builder-official.yml"),
        CreatePipeline("docker-tools-imagebuilder-unofficial", 1513,
            "eng/pipelines/dotnet-buildtools-image-builder-unofficial.yml"),
        CreatePipeline("push-common-updates", 630,
            "eng/pipelines/push-common-updates.yml"),
        CreatePipeline("dotnet-docker-tools-cg-detection", 964,
            "eng/pipelines/cg-detection.yml"),
    ];

    // ── info command data ──────────────────────────────────────────────

    /// <summary>
    /// Variables returned by <c>pipelinemon info</c> for the unofficial imagebuilder pipeline.
    /// </summary>
    public static IReadOnlyList<PipelineVariableInfo> SampleVariables =>
    [
        new("DisableDockerDetector", "true", IsSecret: false, AllowOverride: false),
        new("imageBuilder.pathArgs", "", IsSecret: false, AllowOverride: true),
        new("imageBuilder.queueArgs", "", IsSecret: false, AllowOverride: true),
        new("stages", "build;test;publish", IsSecret: false, AllowOverride: true),
    ];

    /// <summary>
    /// Parameters returned by <c>pipelinemon info</c> for the unofficial imagebuilder pipeline.
    /// </summary>
    public static IReadOnlyList<PipelineParameter> SampleParameters =>
    [
        new()
        {
            Name = "sourceBuildPipelineRunId",
            Type = "string",
            Default = "$(Build.BuildId)",
            DisplayName = "Source build pipeline run ID.",
        },
        new()
        {
            Name = "bootstrapImageBuilder",
            Type = "boolean",
            Default = "false",
            DisplayName = "Build ImageBuilder from source.",
        },
    ];

    /// <summary>
    /// A <see cref="PipelineInfoView"/> built from the real <c>info</c> output.
    /// </summary>
    public static AzurePipelinesTool.Display.PipelineInfoView SamplePipelineInfoView => new()
    {
        Name = "docker-tools-imagebuilder-unofficial",
        Id = 1513,
        RelativePath = "eng/pipelines/dotnet-buildtools-image-builder-unofficial.yml",
        Organization = "dnceng",
        Project = "internal",
        Repository = "dotnet-docker-tools",
        Variables = SampleVariables
            .Select(AzurePipelinesTool.Display.VariableRowView.From)
            .ToList(),
        Parameters = SampleParameters
            .Select(AzurePipelinesTool.Display.ParameterRowView.From)
            .ToList(),
    };

    // ── status / wait command data ─────────────────────────────────────

    /// <summary>
    /// A completed, fully-succeeded timeline.
    /// </summary>
    public static BuildTimelineInfo SucceededTimeline => new(
    [
        new TimelineStageInfo("Build", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
        [
            new TimelineJobInfo("Build linux-amd64", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
            [
                new TimelineTaskInfo("Initialize job", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, 101),
                new TimelineTaskInfo("Checkout", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 2, 102),
                new TimelineTaskInfo("Build images", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 3, 103),
            ]),
            new TimelineJobInfo("Build linux-arm64", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 2, null,
            [
                new TimelineTaskInfo("Initialize job", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, 201),
                new TimelineTaskInfo("Build images", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 2, 202),
            ]),
        ]),
        new TimelineStageInfo("Test", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 2, null,
        [
            new TimelineJobInfo("Test linux-amd64", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
            [
                new TimelineTaskInfo("Run tests", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, 301),
            ]),
        ]),
        new TimelineStageInfo("Publish", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 3, null,
        [
            new TimelineJobInfo("Publish images", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
            [
                new TimelineTaskInfo("Push to ACR", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, 401),
            ]),
        ]),
    ]);

    /// <summary>
    /// A timeline with mixed results: Build succeeded, Test failed, Publish skipped.
    /// </summary>
    public static BuildTimelineInfo FailedTimeline => new(
    [
        new TimelineStageInfo("Build", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
        [
            new TimelineJobInfo("Build linux-amd64", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
            [
                new TimelineTaskInfo("Build images", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, 103),
            ]),
        ]),
        new TimelineStageInfo("Test", TimelineRecordStatus.Completed, PipelineRunResult.Failed, 2, null,
        [
            new TimelineJobInfo("Test linux-amd64", TimelineRecordStatus.Completed, PipelineRunResult.Failed, 1, null,
            [
                new TimelineTaskInfo("Run tests", TimelineRecordStatus.Completed, PipelineRunResult.Failed, 1, 301),
            ]),
        ]),
        new TimelineStageInfo("Publish", TimelineRecordStatus.Completed, PipelineRunResult.Skipped, 3, null,
        [
            new TimelineJobInfo("Publish images", TimelineRecordStatus.Completed, PipelineRunResult.Skipped, 1, null,
            [
                new TimelineTaskInfo("Push to ACR", TimelineRecordStatus.Completed, PipelineRunResult.Skipped, 1, 401),
            ]),
        ]),
    ]);

    /// <summary>
    /// An in-progress timeline: Build completed, Test running, Publish pending.
    /// </summary>
    public static BuildTimelineInfo InProgressTimeline => new(
    [
        new TimelineStageInfo("Build", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
        [
            new TimelineJobInfo("Build linux-amd64", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, null,
            [
                new TimelineTaskInfo("Build images", TimelineRecordStatus.Completed, PipelineRunResult.Succeeded, 1, 103),
            ]),
        ]),
        new TimelineStageInfo("Test", TimelineRecordStatus.InProgress, PipelineRunResult.None, 2, null,
        [
            new TimelineJobInfo("Test linux-amd64", TimelineRecordStatus.InProgress, PipelineRunResult.None, 1, null,
            [
                new TimelineTaskInfo("Run tests", TimelineRecordStatus.InProgress, PipelineRunResult.None, 1, null),
            ]),
        ]),
        new TimelineStageInfo("Publish", TimelineRecordStatus.Pending, PipelineRunResult.None, 3, null,
        [
            new TimelineJobInfo("Publish images", TimelineRecordStatus.Pending, PipelineRunResult.None, 1, null,
            [
                new TimelineTaskInfo("Push to ACR", TimelineRecordStatus.Pending, PipelineRunResult.None, 1, null),
            ]),
        ]),
    ]);

    // ── helpers ─────────────────────────────────────────────────────────

    private static LocalPipelineInfo CreatePipeline(string name, int id, string relativePath) => new(
        Name: name,
        DefinitionFile: new FileInfo(Path.Combine(@"D:\src\docker-tools-playground", relativePath)),
        Id: new PipelineId(id),
        RelativePath: relativePath,
        Organization: new OrganizationInfo("dnceng", new Uri("https://dev.azure.com/dnceng")),
        Project: new ProjectInfo("internal"),
        Repository: new RepositoryInfo("dotnet-docker-tools")
    );
}
