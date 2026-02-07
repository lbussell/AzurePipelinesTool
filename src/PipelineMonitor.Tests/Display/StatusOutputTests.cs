// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;
using PipelineMonitor.AzureDevOps;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class StatusOutputTests
{
    [TestMethod]
    public void StatusTree_SucceededTimeline_ContainsAllStages()
    {
        var output = RenderTree(TestData.SucceededTimeline);

        StringAssert.Contains(output, "Build");
        StringAssert.Contains(output, "Test");
        StringAssert.Contains(output, "Publish");
    }

    [TestMethod]
    public void StatusTree_SucceededTimeline_ShowsSucceededLabels()
    {
        var output = RenderTree(TestData.SucceededTimeline);

        StringAssert.Contains(output, "Succeeded");
    }

    [TestMethod]
    public void StatusTree_FailedTimeline_ShowsMixedResults()
    {
        var output = RenderTree(TestData.FailedTimeline);

        StringAssert.Contains(output, "Succeeded");
        StringAssert.Contains(output, "Failed");
        StringAssert.Contains(output, "Skipped");
    }

    [TestMethod]
    public void StatusTree_InProgressTimeline_ShowsRunningAndPending()
    {
        var output = RenderTree(TestData.InProgressTimeline);

        StringAssert.Contains(output, "Succeeded");
        StringAssert.Contains(output, "Running");
        StringAssert.Contains(output, "Pending");
    }

    [TestMethod]
    public void StatusSummary_SucceededTimeline_ShowsAllStagesComplete()
    {
        var timeline = TestData.SucceededTimeline;
        var completedStages = timeline.Stages.Count(s => s.State == TimelineRecordStatus.Completed);
        var totalStages = timeline.Stages.Count;

        var output = RenderSummary(timeline);

        StringAssert.Contains(output, $"{completedStages}/{totalStages} stages complete");
    }

    [TestMethod]
    public void StatusSummary_InProgressTimeline_ShowsPartialCompletion()
    {
        var timeline = TestData.InProgressTimeline;
        var completedStages = timeline.Stages.Count(s => s.State == TimelineRecordStatus.Completed);
        var totalStages = timeline.Stages.Count;

        var output = RenderSummary(timeline);

        StringAssert.Contains(output, $"{completedStages}/{totalStages} stages complete");
    }

    [TestMethod]
    public void StatusTree_Depth1_ShowsStagesOnly()
    {
        var nodes = TestData.SucceededTimeline.Stages
            .Select(stage =>
            {
                var completedJobs = stage.Jobs.Count(j => j.State == TimelineRecordStatus.Completed);
                var totalJobs = stage.Jobs.Count;
                var label = $"{stage.Name} (Succeeded) — Jobs: {completedJobs}/{totalJobs} complete";
                return new TreeNode(label);
            })
            .ToList();

        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);
        markout.WriteTree(nodes);
        markout.Flush();

        var output = writer.ToString();
        StringAssert.Contains(output, "Build");
        Assert.DoesNotContain(output, "Build linux-amd64", "Depth 1 should not show jobs");
    }

    [TestMethod]
    public void StatusTree_Depth2_ShowsStagesAndJobs()
    {
        var output = RenderTree(TestData.SucceededTimeline);

        StringAssert.Contains(output, "Build");
        StringAssert.Contains(output, "Build linux-amd64");
        StringAssert.Contains(output, "Build linux-arm64");
        Assert.DoesNotContain(output, "Initialize job", "Depth 2 should not show tasks");
    }

    [TestMethod]
    public void StatusTree_Depth3_ShowsStagesJobsAndTasks()
    {
        var output = RenderTreeAtDepth(TestData.SucceededTimeline, 3);

        StringAssert.Contains(output, "Build");
        StringAssert.Contains(output, "Build linux-amd64");
        StringAssert.Contains(output, "Initialize job");
        StringAssert.Contains(output, "Checkout");
        StringAssert.Contains(output, "Build images");
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static string RenderTree(BuildTimelineInfo timeline, int depth = 2)
        => RenderTreeAtDepth(timeline, depth);

    private static string RenderTreeAtDepth(BuildTimelineInfo timeline, int depth)
    {
        var stageNodes = timeline.Stages
            .Select(stage => BuildStageNode(stage, depth))
            .ToList();

        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);
        markout.WriteTree(stageNodes);
        markout.Flush();
        return writer.ToString();
    }

    private static string RenderSummary(BuildTimelineInfo timeline)
    {
        var completedStages = timeline.Stages.Count(s => s.State == TimelineRecordStatus.Completed);
        var totalStages = timeline.Stages.Count;
        var overallState = timeline.Stages.Any(s => s.State == TimelineRecordStatus.InProgress) ? "Running"
            : timeline.Stages.All(s => s.State == TimelineRecordStatus.Completed) ? "Succeeded"
            : "Pending";

        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);
        markout.WriteParagraph($"{overallState} — {completedStages}/{totalStages} stages complete");
        markout.Flush();
        return writer.ToString();
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
