// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Microsoft.TeamFoundation.Build.WebApi;

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Hierarchical representation of a build timeline parsed from flat <see cref="TimelineRecord"/> entries.
/// </summary>
internal sealed record BuildTimelineInfo(
    ImmutableList<TimelineStageInfo> Stages
)
{
    /// <summary>
    /// Parses a flat list of <see cref="TimelineRecord"/> entries into a hierarchical timeline.
    /// Records are linked by <see cref="TimelineRecord.ParentId"/> and grouped by RecordType.
    /// </summary>
    public static BuildTimelineInfo Parse(IList<TimelineRecord> records)
    {
        var byId = records.ToDictionary(r => r.Id);
        var childrenOf = records
            .Where(r => r.ParentId.HasValue)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Order).ToList());

        var stages = records
            .Where(r => r.RecordType == "Stage")
            .OrderBy(r => r.Order)
            .Select(stage => BuildStage(stage, childrenOf))
            .ToImmutableList();

        return new BuildTimelineInfo(stages);
    }

    private static TimelineStageInfo BuildStage(
        TimelineRecord stage,
        Dictionary<Guid, List<TimelineRecord>> childrenOf)
    {
        // Jobs may be nested under a Phase record which is under the Stage.
        // Collect all Job records that are descendants of this stage.
        var jobs = GetDescendants(stage.Id, childrenOf, "Job")
            .OrderBy(r => r.Order)
            .Select(job => BuildJob(job, childrenOf))
            .ToImmutableList();

        return new TimelineStageInfo(
            Name: stage.Name,
            State: ToState(stage.State),
            Result: ToResult(stage.Result),
            Order: stage.Order,
            Jobs: jobs);
    }

    private static TimelineJobInfo BuildJob(
        TimelineRecord job,
        Dictionary<Guid, List<TimelineRecord>> childrenOf)
    {
        var tasks = GetDescendants(job.Id, childrenOf, "Task")
            .OrderBy(r => r.Order)
            .Select(task => new TimelineTaskInfo(
                Name: task.Name,
                State: ToState(task.State),
                Result: ToResult(task.Result),
                Order: task.Order))
            .ToImmutableList();

        return new TimelineJobInfo(
            Name: job.Name,
            State: ToState(job.State),
            Result: ToResult(job.Result),
            Order: job.Order,
            Tasks: tasks);
    }

    /// <summary>
    /// Finds all descendant records of the given type under a parent, traversing intermediate nodes.
    /// </summary>
    private static IEnumerable<TimelineRecord> GetDescendants(
        Guid parentId,
        Dictionary<Guid, List<TimelineRecord>> childrenOf,
        string recordType)
    {
        if (!childrenOf.TryGetValue(parentId, out var children))
            yield break;

        foreach (var child in children)
        {
            if (child.RecordType == recordType)
                yield return child;
            else
            {
                // Recurse through intermediate nodes (e.g., Phase between Stage and Job)
                foreach (var descendant in GetDescendants(child.Id, childrenOf, recordType))
                    yield return descendant;
            }
        }
    }

    private static TimelineRecordStatus ToState(TimelineRecordState? state) =>
        state switch
        {
            TimelineRecordState.Completed => TimelineRecordStatus.Completed,
            TimelineRecordState.InProgress => TimelineRecordStatus.InProgress,
            TimelineRecordState.Pending => TimelineRecordStatus.Pending,
            _ => TimelineRecordStatus.Unknown,
        };

    private static PipelineRunResult ToResult(TaskResult? result) =>
        result switch
        {
            TaskResult.Succeeded => PipelineRunResult.Succeeded,
            TaskResult.SucceededWithIssues => PipelineRunResult.PartiallySucceeded,
            TaskResult.Failed => PipelineRunResult.Failed,
            TaskResult.Canceled => PipelineRunResult.Canceled,
            TaskResult.Skipped => PipelineRunResult.Skipped,
            _ => PipelineRunResult.None,
        };
}

internal enum TimelineRecordStatus
{
    Unknown,
    Pending,
    InProgress,
    Completed,
}

internal sealed record TimelineStageInfo(
    string Name,
    TimelineRecordStatus State,
    PipelineRunResult Result,
    int? Order,
    ImmutableList<TimelineJobInfo> Jobs
);

internal sealed record TimelineJobInfo(
    string Name,
    TimelineRecordStatus State,
    PipelineRunResult Result,
    int? Order,
    ImmutableList<TimelineTaskInfo> Tasks
);

internal sealed record TimelineTaskInfo(
    string Name,
    TimelineRecordStatus State,
    PipelineRunResult Result,
    int? Order
);
