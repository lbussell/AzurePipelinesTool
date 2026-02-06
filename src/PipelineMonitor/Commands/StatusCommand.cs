// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using Spectre.Console;

namespace PipelineMonitor.Commands;

internal sealed class StatusCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelinesService pipelinesService,
    BuildIdResolver buildIdResolver,
    IEnvironment environment
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly BuildIdResolver _buildIdResolver = buildIdResolver;
    private readonly IEnvironment _environment = environment;

    /// <summary>
    /// Show the status of a pipeline run.
    /// </summary>
    /// <param name="buildIdOrUrl">Build ID or Azure DevOps build results URL.</param>
    /// <param name="stage">Filter to a specific stage.</param>
    /// <param name="job">Filter to a specific job (requires --stage).</param>
    [Command("status")]
    public async Task ExecuteAsync([Argument] string buildIdOrUrl, string? stage = null, string? job = null)
    {
        if (job is not null && stage is null)
            throw new UserFacingException("--job requires --stage to be specified.");

        var (org, project, buildId) = await _buildIdResolver.ResolveAsync(buildIdOrUrl);

        var timeline = await _pipelinesService.GetBuildTimelineAsync(org, project, buildId);

        if (stage is null)
            DisplayOverview(timeline, buildId);
        else if (job is null)
            DisplayStage(timeline, stage);
        else
            DisplayJob(timeline, stage, job);
    }

    private void DisplayOverview(BuildTimelineInfo timeline, int buildId)
    {
        var completedStages = timeline.Stages.Count(s => s.State == TimelineRecordStatus.Completed);
        var totalStages = timeline.Stages.Count;

        var overallState = timeline.Stages.Any(s => s.State == TimelineRecordStatus.InProgress)
            ? "Running"
            : timeline.Stages.All(s => s.State == TimelineRecordStatus.Completed)
                ? GetOverallResult(timeline)
                : "Pending";

        _ansiConsole.MarkupLineInterpolated($"[bold]{overallState}[/] - {completedStages}/{totalStages} Stages complete");
        _ansiConsole.WriteLine();

        foreach (var stageInfo in timeline.Stages)
        {
            var stateLabel = GetStateLabel(stageInfo.State, stageInfo.Result);
            var completedJobs = stageInfo.Jobs.Count(j => j.State == TimelineRecordStatus.Completed);
            var totalJobs = stageInfo.Jobs.Count;
            _ansiConsole.WriteLine($"{stageInfo.Name} - {stateLabel} (Jobs: {completedJobs}/{totalJobs} complete)");
        }

        var isRunning = overallState is "Running" or "Pending";
        if (isRunning)
        {
            _ansiConsole.WriteLine();
            _interactionService.DisplaySubtleMessage($"To cancel, run: `cancel {buildId}`");
        }
    }

    private void DisplayStage(BuildTimelineInfo timeline, string stageName)
    {
        var stageInfo = timeline.Stages.FirstOrDefault(
            s => s.Name.Equals(stageName, StringComparison.OrdinalIgnoreCase));

        if (stageInfo is null)
        {
            var available = string.Join(", ", timeline.Stages.Select(s => s.Name));
            throw new UserFacingException($"Stage '{stageName}' not found. Available stages: {available}");
        }

        var stateLabel = GetStateLabel(stageInfo.State, stageInfo.Result);
        var completedJobs = stageInfo.Jobs.Count(j => j.State == TimelineRecordStatus.Completed);
        var totalJobs = stageInfo.Jobs.Count;

        _ansiConsole.MarkupLineInterpolated($"[bold]{stageInfo.Name}[/] - {stateLabel} (Jobs: {completedJobs}/{totalJobs} complete)");
        _ansiConsole.WriteLine();

        foreach (var jobInfo in stageInfo.Jobs)
        {
            var jobState = GetStateLabel(jobInfo.State, jobInfo.Result);
            var completedTasks = jobInfo.Tasks.Count(t => t.State == TimelineRecordStatus.Completed);
            var totalTasks = jobInfo.Tasks.Count;
            _ansiConsole.WriteLine($"{jobInfo.Name} - {jobState} (Tasks: {completedTasks}/{totalTasks} complete)");
        }
    }

    private void DisplayJob(BuildTimelineInfo timeline, string stageName, string jobName)
    {
        var stageInfo = timeline.Stages.FirstOrDefault(
            s => s.Name.Equals(stageName, StringComparison.OrdinalIgnoreCase));

        if (stageInfo is null)
        {
            var available = string.Join(", ", timeline.Stages.Select(s => s.Name));
            throw new UserFacingException($"Stage '{stageName}' not found. Available stages: {available}");
        }

        var jobInfo = stageInfo.Jobs.FirstOrDefault(
            j => j.Name.Equals(jobName, StringComparison.OrdinalIgnoreCase));

        if (jobInfo is null)
        {
            var available = string.Join(", ", stageInfo.Jobs.Select(j => j.Name));
            throw new UserFacingException($"Job '{jobName}' not found in stage '{stageName}'. Available jobs: {available}");
        }

        var jobState = GetStateLabel(jobInfo.State, jobInfo.Result);
        var completedTasks = jobInfo.Tasks.Count(t => t.State == TimelineRecordStatus.Completed);
        var totalTasks = jobInfo.Tasks.Count;

        _ansiConsole.MarkupLineInterpolated($"[bold]{stageInfo.Name}[/] > [bold]{jobInfo.Name}[/] - {jobState} (Tasks: {completedTasks}/{totalTasks} complete)");
        _ansiConsole.WriteLine();

        foreach (var taskInfo in jobInfo.Tasks)
        {
            var taskState = GetStateLabel(taskInfo.State, taskInfo.Result);
            _ansiConsole.WriteLine($"{taskInfo.Name} - {taskState}");
        }
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

    /// <summary>
    /// Derives the overall result label from the worst stage result when all stages are completed.
    /// </summary>
    private static string GetOverallResult(BuildTimelineInfo timeline)
    {
        var worstResult = timeline.Stages
            .Select(s => s.Result)
            .Aggregate(PipelineRunResult.None, WorstOf);

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
