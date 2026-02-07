// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using ConsoleAppFramework;
using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.Display;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

internal sealed class WaitCommand(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    PipelinesService pipelinesService,
    BuildIdResolver buildIdResolver
)
{
    private const int InitialIntervalSeconds = 5;
    private const int IntervalIncrementSeconds = 5;
    private const int MaxIntervalSeconds = 30;

    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly BuildIdResolver _buildIdResolver = buildIdResolver;

    /// <summary>
    /// Wait for a pipeline run to complete.
    /// </summary>
    /// <param name="buildIdOrUrl">Build ID or Azure DevOps build results URL.</param>
    /// <param name="failOnError">-f, Exit with non-zero code if the pipeline fails or is canceled.</param>
    [Command("wait")]
    public async Task ExecuteAsync(
        [Argument] string buildIdOrUrl,
        bool failOnError = false,
        CancellationToken cancellationToken = default
    )
    {
        var (org, project, buildId) = await _buildIdResolver.ResolveAsync(buildIdOrUrl);

        _ansiConsole.WriteLine($"Waiting for pipeline run {buildId}...");
        _ansiConsole.WriteLine();

        var stopwatch = Stopwatch.StartNew();
        var intervalSeconds = InitialIntervalSeconds;

        while (true)
        {
            var timeline = await _pipelinesService.GetBuildTimelineAsync(org, project, buildId, cancellationToken);
            var isComplete = timeline.Stages.All(s => s.State == TimelineRecordStatus.Completed);

            if (isComplete)
            {
                DisplayFinalSummary(timeline, buildId, stopwatch.Elapsed);

                if (failOnError && HasFailure(timeline))
                    Environment.ExitCode = 1;

                return;
            }

            DisplayTimelineProgress(timeline, stopwatch.Elapsed, intervalSeconds);

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            intervalSeconds = Math.Min(intervalSeconds + IntervalIncrementSeconds, MaxIntervalSeconds);
        }
    }

    private void DisplayFinalSummary(BuildTimelineInfo timeline, int buildId, TimeSpan elapsed)
    {
        var overallResult = GetOverallResult(timeline);

        _ansiConsole.WriteLine($"{overallResult} - {FormatElapsed(elapsed)} elapsed");
        _ansiConsole.WriteLine();

        foreach (var stage in timeline.Stages)
        {
            var stateLabel = GetStateLabel(stage.State, stage.Result);
            var completedJobs = stage.Jobs.Count(j => j.State == TimelineRecordStatus.Completed);
            var totalJobs = stage.Jobs.Count;
            _ansiConsole.WriteLine($"{stage.Name} - {stateLabel} (Jobs: {completedJobs}/{totalJobs} complete)");
        }

        _ansiConsole.WriteLine();
        _interactionService.DisplaySubtleMessage($"View details: `status {buildId}`");
    }

    private void DisplayTimelineProgress(BuildTimelineInfo timeline, TimeSpan elapsed, int nextCheckSeconds)
    {
        var allJobs = timeline.Stages.SelectMany(s => s.Jobs).ToList();

        _ansiConsole.WriteLine(FormatStatusCounts("Stages", timeline.Stages.Select(s => s.State)));
        _ansiConsole.WriteLine(FormatStatusCounts("Jobs", allJobs.Select(j => j.State)));
        _ansiConsole.WriteLine($"Elapsed: {FormatElapsed(elapsed)}. Next check in {nextCheckSeconds}s...");
        _ansiConsole.WriteLine();
    }

    private static string FormatStatusCounts(string label, IEnumerable<TimelineRecordStatus> states)
    {
        var counts = states.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());

        List<string> parts = [];
        if (counts.TryGetValue(TimelineRecordStatus.Completed, out var completed))
            parts.Add($"{completed} completed");
        if (counts.TryGetValue(TimelineRecordStatus.InProgress, out var inProgress))
            parts.Add($"{inProgress} in progress");
        if (counts.TryGetValue(TimelineRecordStatus.Pending, out var pending))
            parts.Add($"{pending} pending");

        return $"{label}: {string.Join(", ", parts)}";
    }

    private static bool HasFailure(BuildTimelineInfo timeline) =>
        timeline.Stages.Any(s => s.Result is PipelineRunResult.Failed or PipelineRunResult.Canceled);

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

    internal static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s"
            : elapsed.TotalMinutes >= 1
                ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
                : $"{elapsed.Seconds}s";
}
