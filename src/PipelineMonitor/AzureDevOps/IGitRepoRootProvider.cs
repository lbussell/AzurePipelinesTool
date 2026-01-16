// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Provides access to the Git repository root for the current working directory.
/// </summary>
internal interface IGitRepoRootProvider
{
    /// <summary>
    /// Gets the full path to the Git repository root.
    /// </summary>
    Task<string?> GetRepoRootAsync(CancellationToken cancellationToken = default);
}
