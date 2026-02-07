// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;
using PipelineMonitor.AzureDevOps;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class StatusOutputTests : VerifyBase
{
    [TestMethod]
    public Task StatusTree_Succeeded_Depth2() =>
        Verify(RenderStatusOutput(TestData.SucceededTimeline, depth: 2));

    [TestMethod]
    public Task StatusTree_Failed_Depth2() =>
        Verify(RenderStatusOutput(TestData.FailedTimeline, depth: 2));

    [TestMethod]
    public Task StatusTree_InProgress_Depth2() =>
        Verify(RenderStatusOutput(TestData.InProgressTimeline, depth: 2));

    [TestMethod]
    public Task StatusTree_Succeeded_Depth1() =>
        Verify(RenderStatusOutput(TestData.SucceededTimeline, depth: 1));

    [TestMethod]
    public Task StatusTree_Succeeded_Depth3() =>
        Verify(RenderStatusOutput(TestData.SucceededTimeline, depth: 3));

    // ── helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Renders the full status command output: summary paragraph + timeline tree.
    /// Mirrors the rendering logic in <see cref="PipelineMonitor.Commands.StatusCommand"/>.
    /// </summary>
    private static string RenderStatusOutput(BuildTimelineInfo timeline, int depth)
    {
        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);

        var completedStages = timeline.Stages.Count(s => s.State == TimelineRecordStatus.Completed);
        var totalStages = timeline.Stages.Count;
        var overallState = timeline.Stages.Any(s => s.State == TimelineRecordStatus.InProgress) ? "Running"
            : timeline.Stages.All(s => s.State == TimelineRecordStatus.Completed) ? GetOverallResult(timeline)
            : "Pending";

        markout.WriteParagraph($"{overallState} — {completedStages}/{totalStages} stages complete");

        var stageNodes = timeline.Stages
            .Select(stage => BuildStageNode(stage, depth))
            .ToList();
        markout.WriteTree(stageNodes);
        markout.Flush();

        return writer.ToString();
    }

    private static string GetOverallResult(BuildTimelineInfo timeline)
    {
        var results = timeline.Stages.Select(s => s.Result).ToList();
        if (results.Any(r => r == PipelineRunResult.Failed)) return "Failed";
        if (results.Any(r => r == PipelineRunResult.Canceled)) return "Canceled";
        if (results.Any(r => r == PipelineRunResult.PartiallySucceeded)) return "Partially Succeeded";
        return results.All(r => r is PipelineRunResult.Succeeded or PipelineRunResult.Skipped) ? "Succeeded" : "Completed";
    }

    private static TreeNode BuildStageNode(TimelineStageInfo stage, int depth)
    {
        var completedJobs = stage.Jobs.Count(j => j.State == TimelineRecordStatus.Completed);
        var totalJobs = stage.Jobs.Count;
        var label = $"{stage.Name} ({GetStateLabel(stage.State, stage.Result)}) — Jobs: {completedJobs}/{totalJobs} complete";

        List<TreeNode>? children = depth >= 2
            ? stage.Jobs.Select(job => BuildJobNode(job, depth)).ToList()
            : null;

        return new TreeNode(label, children: children);
    }

    private static TreeNode BuildJobNode(TimelineJobInfo job, int depth)
    {
        var label = $"{job.Name} ({GetStateLabel(job.State, job.Result)})";

        List<TreeNode>? children = depth >= 3
            ? job.Tasks.Select(BuildTaskNode).ToList()
            : null;

        return new TreeNode(label, children: children);
    }

    private static TreeNode BuildTaskNode(TimelineTaskInfo task) =>
        new($"{task.Name} ({GetStateLabel(task.State, task.Result)})");

    private static string GetStateLabel(TimelineRecordStatus state, PipelineRunResult result) =>
        state switch
        {
            TimelineRecordStatus.Completed => result switch
            {
                PipelineRunResult.Succeeded => "Succeeded",
                PipelineRunResult.PartiallySucceeded => "Partially Succeeded",
                PipelineRunResult.Failed => "Failed",
                PipelineRunResult.Canceled => "Canceled",
                PipelineRunResult.Skipped => "Skipped",
                _ => "Completed",
            },
            TimelineRecordStatus.InProgress => "Running",
            TimelineRecordStatus.Pending => "Pending",
            _ => "Unknown",
        };
}
