// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace AzurePipelinesTool;

// Common models for Azure DevOps concepts.

internal sealed record OrganizationInfo(string Name, Uri Uri);

internal sealed record ProjectInfo(string Name);

internal sealed record RepositoryInfo(string Name, Guid? Id = null);

internal sealed record PipelineInfo(string Name, PipelineId Id, string Url, string Folder);

internal readonly record struct PipelineId(int Value);

internal sealed record LocalPipelineInfo(
    string Name,
    FileInfo DefinitionFile,
    PipelineId Id,
    string RelativePath,
    OrganizationInfo Organization,
    ProjectInfo Project,
    RepositoryInfo Repository
);

internal readonly record struct RunId(int Value);

internal sealed record PipelineRunInfo(
    string Name,
    RunId Id,
    string State,
    PipelineRunResult Result,
    DateTimeOffset? Started,
    DateTimeOffset? Finished,
    CommitInfo? Commit,
    string Url,
    IEnumerable<StageInfo> Stages
);

internal enum PipelineRunResult
{
    None,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Canceled,
    Skipped,
}

internal sealed record CommitInfo(string Sha, string Message, string Author, DateTime? Date);

internal sealed record StageInfo(string Name, string State, PipelineRunResult Result);

internal sealed record PipelineVariableInfo(string Name, string Value, bool IsSecret, bool AllowOverride);

internal sealed record QueuedPipelineRunInfo(RunId Id, string Name, string WebUrl);
