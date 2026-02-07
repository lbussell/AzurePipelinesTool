// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace AzurePipelinesTool.Git;

/// <summary>
/// Provides access to Git remote URLs from the current working directory.
/// </summary>
internal interface IGitRemoteUrlProvider
{
    /// <summary>
    /// Gets all Git remote URLs from the current directory.
    /// Returns a dictionary where keys are "{remote_name}({fetch|push})" and values are URLs.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>?> GetRemotesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the best remote URL for Azure DevOps detection.
    /// Prefers 'origin (push)', then other '(push)' remotes.
    /// </summary>
    /// <param name="validationFunction">Optional function to filter candidate URLs.</param>
    Task<string?> GetRemoteUrlAsync(
        Func<string, bool>? validationFunction = null,
        CancellationToken cancellationToken = default
    );
}
