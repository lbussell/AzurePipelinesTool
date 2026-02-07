// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.Display;
using ConsoleAppFramework;
using Markout;
using Spectre.Console;
using TreeNode = Markout.TreeNode;

namespace AzurePipelinesTool.Commands;

internal sealed class StatusCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelinesService pipelinesService,
    BuildIdResolver buildIdResolver
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly BuildIdResolver _buildIdResolver = buildIdResolver;

    /// <summary>
    /// Show the status of a pipeline run.
    /// </summary>
    /// <param name="buildIdOrUrl">Build ID or Azure DevOps build results URL.</param>
    /// <param name="depth">-d, Tree nesting depth: 1=stages, 2=stages+jobs, 3=stages+jobs+tasks.</param>
    [Command("status")]
    public async Task ExecuteAsync([Argument] string buildIdOrUrl, int depth = 2)
    {
        if (depth is < 1 or > 3)
            throw new UserFacingException("--depth must be between 1 and 3.");

        var (org, project, buildId) = await _buildIdResolver.ResolveAsync(buildIdOrUrl);
        var timeline = await _pipelinesService.GetBuildTimelineAsync(org, project, buildId);

        var writer = new MarkoutWriter(_ansiConsole.Profile.Out.Writer);
        WriteSummary(writer, timeline, buildId);
        WriteTimelineTree(writer, timeline, depth);
        writer.Flush();
    }

    private void WriteSummary(MarkoutWriter writer, BuildTimelineInfo timeline, int buildId)
    {
        var completedStages = timeline.Stages.Count(s => s.State == TimelineRecordStatus.Completed);
        var totalStages = timeline.Stages.Count;
        var overallState = GetOverallState(timeline);

        writer.WriteParagraph($"{overallState} — {completedStages}/{totalStages} stages complete");

        var isRunning = overallState is "Running" or "Pending";
        if (isRunning)
            _interactionService.DisplaySubtleMessage($"To cancel, run: `cancel {buildId}`");
    }

    private static void WriteTimelineTree(MarkoutWriter writer, BuildTimelineInfo timeline, int depth)
    {
        var stageNodes = timeline.Stages.Select(stage => BuildStageNode(stage, depth)).ToList();

        writer.WriteTree(stageNodes);
    }

    private static TreeNode BuildStageNode(TimelineStageInfo stage, int depth)
    {
        var completedJobs = stage.Jobs.Count(j => j.State == TimelineRecordStatus.Completed);
        var totalJobs = stage.Jobs.Count;
        var label =
            $"{stage.Name} ({GetStateLabel(stage.State, stage.Result)}) — Jobs: {completedJobs}/{totalJobs} complete";

        List<TreeNode>? children = depth >= 2 ? stage.Jobs.Select(job => BuildJobNode(job, depth)).ToList() : null;

        return new TreeNode(label, children: children);
    }

    private static TreeNode BuildJobNode(TimelineJobInfo job, int depth)
    {
        var label = $"{job.Name} ({GetStateLabel(job.State, job.Result)})";

        List<TreeNode>? children = depth >= 3 ? job.Tasks.Select(BuildTaskNode).ToList() : null;

        return new TreeNode(label, children: children);
    }

    private static TreeNode BuildTaskNode(TimelineTaskInfo task)
    {
        var label = $"{task.Name} ({GetStateLabel(task.State, task.Result)})";
        return new TreeNode(label);
    }

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

    private static string GetOverallState(BuildTimelineInfo timeline) =>
        timeline.Stages.Any(s => s.State == TimelineRecordStatus.InProgress) ? "Running"
        : timeline.Stages.All(s => s.State == TimelineRecordStatus.Completed) ? GetOverallResult(timeline)
        : "Pending";

    private static string GetOverallResult(BuildTimelineInfo timeline)
    {
        var worstResult = timeline.Stages.Select(s => s.Result).Aggregate(PipelineRunResult.None, WorstOf);

        return worstResult switch
        {
            PipelineRunResult.Succeeded => "Succeeded",
            PipelineRunResult.PartiallySucceeded => "Partially Succeeded",
            PipelineRunResult.Failed => "Failed",
            PipelineRunResult.Canceled => "Canceled",
            PipelineRunResult.Skipped => "Skipped",
            _ => "Completed",
        };
    }

    private static PipelineRunResult WorstOf(PipelineRunResult a, PipelineRunResult b) =>
        Severity(a) > Severity(b) ? a : b;

    private static int Severity(PipelineRunResult result) =>
        result switch
        {
            PipelineRunResult.Skipped => 0,
            PipelineRunResult.Succeeded => 1,
            PipelineRunResult.PartiallySucceeded => 2,
            PipelineRunResult.Canceled => 3,
            PipelineRunResult.Failed => 4,
            _ => -1,
        };
}
