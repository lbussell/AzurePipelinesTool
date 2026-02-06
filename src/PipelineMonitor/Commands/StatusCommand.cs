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
    RepoInfoResolver repoInfoResolver
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly RepoInfoResolver _repoInfoResolver = repoInfoResolver;

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

        var (org, project, buildId) = await ResolveArgumentAsync(buildIdOrUrl);

        var timeline = await _interactionService.ShowLoadingAsync(
            "Fetching timeline...",
            () => _pipelinesService.GetBuildTimelineAsync(org, project, buildId)
        );

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

    private async Task<(OrganizationInfo Org, ProjectInfo Project, int BuildId)> ResolveArgumentAsync(
        string buildIdOrUrl)
    {
        if (TryParseAzureDevOpsUrl(buildIdOrUrl, out var orgName, out var projectName, out var buildId))
        {
            var org = new OrganizationInfo(orgName, new Uri($"https://dev.azure.com/{orgName}"));
            var project = new ProjectInfo(projectName);
            return (org, project, buildId);
        }

        if (!int.TryParse(buildIdOrUrl, out var id))
            throw new UserFacingException(
                $"Invalid argument '{buildIdOrUrl}'. Provide a numeric build ID or an Azure DevOps build results URL.");

        var repoInfo = await _repoInfoResolver.ResolveAsync();
        return repoInfo.Organization is null || repoInfo.Project is null
            ? throw new UserFacingException(
                "Could not detect Azure DevOps organization/project from Git remotes. Use a full build URL instead.")
            : ((OrganizationInfo Org, ProjectInfo Project, int BuildId))(repoInfo.Organization, repoInfo.Project, id);
    }

    /// <summary>
    /// Parses Azure DevOps build URLs in both modern and legacy formats, handling any query parameter
    /// order and URL-encoded org/project names.
    /// </summary>
    private static bool TryParseAzureDevOpsUrl(
        string input,
        out string orgName,
        out string projectName,
        out int buildId)
    {
        orgName = "";
        projectName = "";
        buildId = 0;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            return false;

        // Extract buildId from query string regardless of parameter order
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return false;

        string? buildIdValue = null;
        foreach (var param in query.TrimStart('?').Split('&'))
        {
            var eqIndex = param.IndexOf('=');
            if (eqIndex <= 0)
                continue;
            if (param[..eqIndex].Equals("buildId", StringComparison.OrdinalIgnoreCase))
            {
                buildIdValue = param[(eqIndex + 1)..];
                break;
            }
        }

        if (buildIdValue is null || !int.TryParse(buildIdValue, out buildId))
            return false;

        var pathParts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Modern: dev.azure.com/{org}/{project}/_build/results
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) && pathParts.Length >= 2)
        {
            orgName = Uri.UnescapeDataString(pathParts[0]);
            projectName = Uri.UnescapeDataString(pathParts[1]);
            return true;
        }

        // Legacy: {org}.visualstudio.com/{project}/_build/results
        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) && pathParts.Length >= 1)
        {
            orgName = uri.Host.Split('.')[0];
            projectName = Uri.UnescapeDataString(pathParts[0]);
            return true;
        }

        return false;
    }
}
