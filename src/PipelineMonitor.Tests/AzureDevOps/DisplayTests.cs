// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using PipelineMonitor.AzureDevOps;
using Spectre.Console.Rendering;

namespace PipelineMonitor.Tests.AzureDevOps;

[TestClass]
public class DisplayTests
{
    [TestMethod]
    public void RunDetails_WithBracketsInCommitMessage_DoesNotThrow()
    {
        // Arrange - Create a run with a commit message containing brackets (common in merge commits)
        var run = new PipelineRunInfo(
            Name: "Build Pipeline",
            Id: new RunId(123),
            State: "completed",
            Result: PipelineRunResult.Succeeded,
            Started: DateTimeOffset.UtcNow,
            Finished: DateTimeOffset.UtcNow.AddMinutes(5),
            Commit: new CommitInfo(
                Sha: "abc123def456",
                Message: "Merge branch 'main' into feature-branch",
                Author: "Test User",
                Date: DateTime.UtcNow
            ),
            Url: "https://dev.azure.com/org/project/_build/results?buildId=123",
            Stages: []
        );

        // Act & Assert - Should not throw InvalidOperationException
        var result = run.RunDetails;
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RunDetails_WithSquareBracketsInCommitMessage_DoesNotThrow()
    {
        // Arrange - Create a run with square brackets that could be misinterpreted as markup
        var run = new PipelineRunInfo(
            Name: "Build Pipeline [CI]",
            Id: new RunId(456),
            State: "completed",
            Result: PipelineRunResult.Succeeded,
            Started: DateTimeOffset.UtcNow,
            Finished: DateTimeOffset.UtcNow.AddMinutes(5),
            Commit: new CommitInfo(
                Sha: "xyz789abc012",
                Message: "Fix issue [#123] by updating [main] branch",
                Author: "Test User",
                Date: DateTime.UtcNow
            ),
            Url: "https://dev.azure.com/org/project/_build/results?buildId=456",
            Stages: []
        );

        // Act & Assert - Should not throw InvalidOperationException
        var result = run.RunDetails;
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void SingleLineDisplay_WithBracketsInPipelineName_DoesNotThrow()
    {
        // Arrange - Create a pipeline with brackets in the name
        var pipeline = new LocalPipelineInfo(
            Name: "Build Pipeline [Production]",
            DefinitionFile: new FileInfo("/path/to/azure-pipelines.yml"),
            Id: new PipelineId(789),
            RelativePath: "path/to/[bracketed]/azure-pipelines.yml"
        );

        // Act & Assert - Should not throw InvalidOperationException
        var result = pipeline.SingleLineDisplay;
        Assert.IsNotNull(result);
    }
}
