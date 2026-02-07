// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace AzurePipelinesTool.AzureDevOps;

/// <summary>
/// Result of resolving Azure DevOps repository information from the environment.
/// Fields may be null if they could not be resolved.
/// </summary>
internal sealed record ResolvedRepoInfo(
    OrganizationInfo? Organization,
    ProjectInfo? Project,
    RepositoryInfo? Repository
);
