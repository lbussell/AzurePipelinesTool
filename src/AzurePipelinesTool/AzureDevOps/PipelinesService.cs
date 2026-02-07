// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Runtime.CompilerServices;
using AzurePipelinesTool.Authentication;
using AzurePipelinesTool.Git;
using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace AzurePipelinesTool.AzureDevOps;

internal sealed class PipelinesService(
    VssConnectionProvider vssConnectionProvider,
    RepoInfoResolver repoInfoResolver,
    GitService gitService
)
{
    private readonly VssConnectionProvider _vssConnectionProvider = vssConnectionProvider;
    private readonly RepoInfoResolver _repoInfoResolver = repoInfoResolver;
    private readonly GitService _gitService = gitService;

    public async Task<PipelineInfo> GetPipelineAsync(OrganizationInfo org, ProjectInfo project, PipelineId id)
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();
        var pipeline = await client.GetPipelineAsync(project: project.Name, pipelineId: id.Value);
        var result = new PipelineInfo(pipeline.Name, new PipelineId(pipeline.Id), pipeline.Url, pipeline.Folder);
        return result;
    }

    public async IAsyncEnumerable<LocalPipelineInfo> GetLocalPipelinesAsync(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var repoInfo = await _repoInfoResolver.ResolveAsync(cancellationToken: ct);
        if (repoInfo.Organization is null || repoInfo.Project is null || repoInfo.Repository is null)
        {
            yield break;
        }

        var connection = _vssConnectionProvider.GetConnection(repoInfo.Organization.Uri);
        var buildsClient = connection.GetClient<BuildHttpClient>();

        var buildDefinitions = await buildsClient.GetFullDefinitionsAsync2(
            repositoryId: repoInfo.Repository.Id.ToString(),
            project: repoInfo.Project.Name,
            repositoryType: "TfsGit",
            cancellationToken: ct
        );

        var repoRoot = await _gitService.GetRepoRootAsync(ct);

        foreach (var buildDefinition in buildDefinitions)
        {
            // Ignore non-YAML pipeline definitions for now.
            if (buildDefinition.Process is not YamlProcess yamlBuildProcess)
                continue;

            // Path.Join vs. Path.Combine: YamlProcess.YamlFilename has a leading
            // slash, which causes Path.Combine to ignore the first argument.
            var pipelineFilePath = Path.Join(repoRoot ?? Environment.CurrentDirectory, yamlBuildProcess.YamlFilename);
            var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, pipelineFilePath).Replace('\\', '/');

            yield return new LocalPipelineInfo(
                Name: buildDefinition.Name,
                DefinitionFile: new FileInfo(pipelineFilePath),
                Id: new(buildDefinition.Id),
                RelativePath: relativePath,
                Organization: repoInfo.Organization,
                Project: repoInfo.Project,
                Repository: repoInfo.Repository
            );
        }
    }

    public async IAsyncEnumerable<PipelineInfo> GetAllPipelinesAsync(OrganizationInfo org, ProjectInfo project)
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();

        string? continuationToken = null;
        do
        {
            List<Pipeline> response = await client.ListPipelinesAsync(project.Name, continuationToken, top: 10);

            foreach (var pipeline in response)
            {
                yield return new PipelineInfo(
                    Name: pipeline.Name,
                    Id: new PipelineId(pipeline.Id),
                    Url: pipeline.Url,
                    Folder: pipeline.Folder
                );
            }

            if (response is IPagedList pagedListResponse)
            {
                continuationToken = pagedListResponse.ContinuationToken;
            }
        } while (!string.IsNullOrEmpty(continuationToken));
    }

    public async IAsyncEnumerable<PipelineRunInfo> GetRunsAsync(
        OrganizationInfo org,
        ProjectInfo project,
        PipelineId pipelineId,
        int top = 10,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();
        var buildsClient = connection.GetClient<BuildHttpClient>();

        var builds = await buildsClient.GetBuildsAsync2(
            project: project.Name,
            definitions: [pipelineId.Value],
            top: top,
            cancellationToken: ct
        );

        foreach (var build in builds)
        {
            var changesTask = buildsClient.GetBuildChangesAsync2(project.Name, build.Id);
            var timelineTask = buildsClient.GetBuildTimelineAsync(project.Name, build.Id, cancellationToken: ct);

            var changes = await changesTask;
            var change = changes.FirstOrDefault();
            var commit = change is not null
                ? new CommitInfo(
                    Sha: change.Id,
                    Message: change.Message,
                    Author: change.Author.DisplayName,
                    Date: change.Timestamp
                )
                : null;

            var result = build.Result switch
            {
                BuildResult.Succeeded => PipelineRunResult.Succeeded,
                BuildResult.PartiallySucceeded => PipelineRunResult.PartiallySucceeded,
                BuildResult.Failed => PipelineRunResult.Failed,
                BuildResult.Canceled => PipelineRunResult.Canceled,
                _ => PipelineRunResult.None,
            };

            var timeline = await timelineTask;
            var stages = timeline.Records.Where(r => r.RecordType == "Stage").ToList();
            var stageInfos = stages.Select(stage => new StageInfo(
                Name: stage.Name,
                State: stage.State?.ToString() ?? "Unknown",
                Result: stage.Result switch
                {
                    TaskResult.Succeeded => PipelineRunResult.Succeeded,
                    TaskResult.SucceededWithIssues => PipelineRunResult.PartiallySucceeded,
                    TaskResult.Failed => PipelineRunResult.Failed,
                    TaskResult.Canceled => PipelineRunResult.Canceled,
                    _ => PipelineRunResult.None,
                }
            ));

            yield return new PipelineRunInfo(
                Name: build.BuildNumber,
                Id: new RunId(build.Id),
                State: build.Status?.ToString() ?? "Unknown",
                Result: result,
                Started: build.QueueTime,
                Finished: build.FinishTime,
                Commit: commit,
                Url: build.Url,
                Stages: stageInfos
            );
        }
    }

    public async IAsyncEnumerable<PipelineRunInfo> GetRunsAsync(
        LocalPipelineInfo pipeline,
        int top = 10,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var runs = GetRunsAsync(pipeline.Organization, pipeline.Project, pipeline.Id, top, ct);
        await foreach (var run in runs.WithCancellation(ct))
            yield return run;
    }

    public async Task<IReadOnlyList<PipelineVariableInfo>> GetVariablesAsync(
        LocalPipelineInfo pipeline,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(pipeline.Organization.Uri);
        var buildsClient = connection.GetClient<BuildHttpClient>();

        var buildDefinition = await buildsClient.GetDefinitionAsync(
            project: pipeline.Project.Name,
            definitionId: pipeline.Id.Value,
            cancellationToken: ct
        );

        if (buildDefinition.Variables is null)
            return [];

        var variables = buildDefinition
            .Variables.Select(kvp => new PipelineVariableInfo(
                Name: kvp.Key,
                Value: kvp.Value.Value ?? string.Empty,
                IsSecret: kvp.Value.IsSecret == true,
                AllowOverride: kvp.Value.AllowOverride == true
            ))
            .ToList();

        return variables;
    }

    public async Task<string> PreviewPipelineAsync(
        LocalPipelineInfo pipeline,
        string? refName = null,
        Dictionary<string, string>? templateParameters = null,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(pipeline.Organization.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();

        var runParameters = new RunPipelineParameters { PreviewRun = true };

        if (refName is not null)
        {
            runParameters.Resources = new RunResourcesParameters();
            runParameters.Resources.Repositories["self"] = new RepositoryResourceParameters { RefName = refName };
        }

        if (templateParameters is not null)
        {
            foreach (var (key, value) in templateParameters)
                runParameters.TemplateParameters[key] = value;
        }

        var preview = await client.PreviewAsync(
            runParameters,
            project: pipeline.Project.Name,
            pipelineId: pipeline.Id.Value,
            cancellationToken: ct
        );

        return preview.FinalYaml;
    }

    public async Task<QueuedPipelineRunInfo> RunPipelineAsync(
        LocalPipelineInfo pipeline,
        string? refName = null,
        Dictionary<string, string>? templateParameters = null,
        Dictionary<string, string>? variables = null,
        IReadOnlyCollection<string>? stagesToSkip = null,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(pipeline.Organization.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();

        var runParameters = new RunPipelineParameters();

        if (refName is not null)
        {
            runParameters.Resources = new RunResourcesParameters();
            runParameters.Resources.Repositories["self"] = new RepositoryResourceParameters { RefName = refName };
        }

        if (templateParameters is not null)
        {
            foreach (var (key, value) in templateParameters)
                runParameters.TemplateParameters[key] = value;
        }

        if (variables is not null)
        {
            foreach (var (key, value) in variables)
                runParameters.Variables[key] = CreateVariable(value);
        }

        if (stagesToSkip is not null)
        {
            foreach (var stage in stagesToSkip)
                runParameters.StagesToSkip.Add(stage);
        }

        var run = await client.RunPipelineAsync(
            runParameters,
            project: pipeline.Project.Name,
            pipelineId: pipeline.Id.Value,
            cancellationToken: ct
        );

        var webUrl = GetRunWebUrl(run.Id, pipeline);

        return new QueuedPipelineRunInfo(Id: new RunId(run.Id), Name: run.Name, WebUrl: webUrl);
    }

    private static string GetRunWebUrl(int runId, LocalPipelineInfo pipeline)
    {
        var org = pipeline.Organization.Name;
        var project = pipeline.Project.Name;
        return $"https://dev.azure.com/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(project)}/_build/results?buildId={runId}";
    }

    /// <summary>
    /// Creates a <see cref="Variable"/> instance via reflection because the constructor is not public.
    /// </summary>
    private static Variable CreateVariable(string value)
    {
        var ctor =
            typeof(Variable).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null
            ) ?? throw new InvalidOperationException("Could not find a parameterless constructor on Variable.");

        var variable = (Variable)ctor.Invoke(null);
        variable.Value = value;
        return variable;
    }

    public async Task SetVariablesAsync(
        LocalPipelineInfo pipeline,
        IEnumerable<PipelineVariableInfo> variables,
        bool clearExisting = false,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(pipeline.Organization.Uri);
        var buildsClient = connection.GetClient<BuildHttpClient>();

        var buildDefinition = await buildsClient.GetDefinitionAsync(
            project: pipeline.Project.Name,
            definitionId: pipeline.Id.Value,
            cancellationToken: ct
        );

        if (clearExisting)
        {
            buildDefinition.Variables.Clear();
        }

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue; // Skip variables with null or empty names
            }

            buildDefinition.Variables[variable.Name] = new BuildDefinitionVariable
            {
                Value = variable.Value,
                IsSecret = variable.IsSecret,
                AllowOverride = variable.AllowOverride,
            };
        }

        await buildsClient.UpdateDefinitionAsync(
            definition: buildDefinition,
            project: pipeline.Project.Name,
            definitionId: pipeline.Id.Value,
            cancellationToken: ct
        );
    }

    public async Task<BuildTimelineInfo> GetBuildTimelineAsync(
        OrganizationInfo org,
        ProjectInfo project,
        int buildId,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var buildsClient = connection.GetClient<BuildHttpClient>();

        var timeline = await buildsClient.GetBuildTimelineAsync(
            project: project.Name,
            buildId: buildId,
            cancellationToken: ct
        );

        if (timeline?.Records is null || timeline.Records.Count == 0)
            throw new UserFacingException($"No timeline data found for build {buildId}.");

        return BuildTimelineInfo.Parse(timeline.Records);
    }

    public async Task CancelBuildAsync(
        OrganizationInfo org,
        ProjectInfo project,
        int buildId,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var buildsClient = connection.GetClient<BuildHttpClient>();

        var build = await buildsClient.GetBuildAsync(project: project.Name, buildId: buildId, cancellationToken: ct);

        if (build.Status == BuildStatus.Completed)
            throw new UserFacingException($"Build {buildId} has already completed with result: {build.Result}.");

        if (build.Status == BuildStatus.Cancelling)
            throw new UserFacingException($"Build {buildId} is already being canceled.");

        build.Status = BuildStatus.Cancelling;

#pragma warning disable CS0618 // UpdateBuildAsync compat overload is functionally identical
        await buildsClient.UpdateBuildAsync(build, project.Name, buildId, cancellationToken: ct);
#pragma warning restore CS0618
    }

    public async Task<Stream> GetBuildLogAsync(
        OrganizationInfo org,
        ProjectInfo project,
        int buildId,
        int logId,
        CancellationToken ct = default
    )
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var buildsClient = connection.GetClient<BuildHttpClient>();

        return await buildsClient.GetBuildLogAsync(
            project: project.Name,
            buildId: buildId,
            logId: logId,
            cancellationToken: ct
        );
    }
}
