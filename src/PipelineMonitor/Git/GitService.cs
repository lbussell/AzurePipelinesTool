// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

namespace PipelineMonitor.Git;

/// <summary>
/// Provides access to Git data from the current working directory.
/// </summary>
internal sealed class GitService(IProcessRunner processRunner, ILogger<GitService> logger)
{
    private const string GitExecutable = "git";
    private const string OriginPushKey = "origin(push)";

    private readonly IProcessRunner _processRunner = processRunner;
    private readonly ILogger<GitService> _logger = logger;
    private Dictionary<string, string>? _cachedRemotes;

    public async Task<IReadOnlyDictionary<string, string>?> GetRemotesAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_cachedRemotes is not null)
        {
            return _cachedRemotes;
        }

        try
        {
            // Run: git remote -v
            // Example output:
            // origin  https://dev.azure.com/org/project/_git/repo (fetch)
            // origin  https://dev.azure.com/org/project/_git/repo (push)
            var result = await _processRunner.ExecuteAsync(
                GitExecutable,
                "remote -v",
                cancellationToken: cancellationToken
            );

            _cachedRemotes = [];
            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Each line is: {name}\t{url} ({fetch|push})
                var parts = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    // Key format: "{name}({fetch|push})" matching Python implementation
                    var key = parts[0] + parts[2];
                    _cachedRemotes[key] = parts[1];
                }
            }

            return _cachedRemotes;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Could not detect current remotes based on current working directory");
            _logger.LogDebug(ex, "Exception details");
            return null;
        }
    }

    public async Task<string?> GetRemoteUrlAsync(
        Func<string, bool>? validationFunction = null,
        CancellationToken cancellationToken = default
    )
    {
        var remotes = await GetRemotesAsync(cancellationToken);
        if (remotes is null)
        {
            return null;
        }

        // Prefer origin (push) remote
        if (remotes.TryGetValue(OriginPushKey, out var originUrl))
        {
            if (validationFunction is null || validationFunction(originUrl))
            {
                return originUrl;
            }
        }

        // Fall back to any other (push) remote
        foreach (var (key, url) in remotes)
        {
            if (key != OriginPushKey && key.EndsWith("(push)"))
            {
                if (validationFunction is null || validationFunction(url))
                {
                    return url;
                }
            }
        }

        return null;
    }

    public async Task<string?> GetRepoRootAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner.ExecuteAsync(
                GitExecutable,
                "rev-parse --show-toplevel",
                cancellationToken: cancellationToken
            );
            var root = result.StandardOutput.Trim();
            if (string.IsNullOrEmpty(root))
            {
                _logger.LogInformation("Could not detect git repository root based on current working directory");
                return null;
            }

            return root;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Could not detect git repository root based on current working directory");
            _logger.LogDebug(ex, "Exception details");
            return null;
        }
    }

    /// <summary>
    /// Gets the name of the current branch.
    /// </summary>
    public async Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            "rev-parse --abbrev-ref HEAD",
            cancellationToken: cancellationToken
        );
        var branch = result.StandardOutput.Trim();
        return string.IsNullOrEmpty(branch) ? null : branch;
    }

    /// <summary>
    /// Gets the upstream tracking branch for the current branch.
    /// </summary>
    /// <returns>The upstream branch (e.g., "origin/main"), or null if not set.</returns>
    public async Task<string?> GetUpstreamBranchAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            "rev-parse --abbrev-ref @{upstream}",
            allowNonZeroExitCode: true,
            cancellationToken: cancellationToken
        );
        if (result.ExitCode != 0)
            return null;

        var upstream = result.StandardOutput.Trim();
        return string.IsNullOrEmpty(upstream) ? null : upstream;
    }

    /// <summary>
    /// Gets the URL for a specific remote.
    /// </summary>
    public async Task<string?> GetRemoteUrlByNameAsync(string remoteName, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            $"remote get-url {remoteName}",
            allowNonZeroExitCode: true,
            cancellationToken: cancellationToken
        );
        if (result.ExitCode != 0)
            return null;

        var url = result.StandardOutput.Trim();
        return string.IsNullOrEmpty(url) ? null : url;
    }

    /// <summary>
    /// Gets the number of commits ahead and behind the upstream branch.
    /// </summary>
    /// <returns>A tuple of (ahead, behind) counts, or null if no upstream or error.</returns>
    public async Task<(int Ahead, int Behind)?> GetAheadBehindAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            "rev-list --left-right --count @{upstream}...HEAD",
            allowNonZeroExitCode: true,
            cancellationToken: cancellationToken
        );
        if (result.ExitCode != 0)
            return null;

        // Output format: "behind\tahead"
        var parts = result.StandardOutput.Trim().Split('\t', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead))
            return (ahead, behind);

        return null;
    }

    /// <summary>
    /// Gets the working tree status (uncommitted changes).
    /// </summary>
    public async Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            "status --porcelain",
            cancellationToken: cancellationToken
        );
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int staged = 0,
            modified = 0,
            untracked = 0;

        foreach (var line in lines)
        {
            if (line.Length < 2)
                continue;

            var indexStatus = line[0];
            var workTreeStatus = line[1];

            if (indexStatus == '?' && workTreeStatus == '?')
                untracked++;
            else if (indexStatus != ' ' && indexStatus != '?')
                staged++;
            else if (workTreeStatus != ' ')
                modified++;
        }

        return new WorkingTreeStatus(staged, modified, untracked);
    }

    /// <summary>
    /// Stages all changes (git add -A).
    /// </summary>
    public async Task StageAllAsync(CancellationToken cancellationToken = default)
    {
        await _processRunner.ExecuteAsync(GitExecutable, "add -A", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Commits staged changes with the given message.
    /// </summary>
    public async Task<string> CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        var escapedMessage = message.Replace("\"", "\\\"");
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            $"commit -m \"{escapedMessage}\"",
            cancellationToken: cancellationToken
        );
        return result.StandardOutput.Trim();
    }

    /// <summary>
    /// Pushes to the upstream branch.
    /// </summary>
    public async Task<string> PushAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            "push",
            allowNonZeroExitCode: true,
            cancellationToken: cancellationToken
        );
        if (result.ExitCode != 0)
            throw new UserFacingException($"Git push failed: {result.StandardError.Trim()}");

        // Git push outputs to stderr for progress, stdout for results
        return string.IsNullOrEmpty(result.StandardOutput) ? result.StandardError.Trim() : result.StandardOutput.Trim();
    }

    /// <summary>
    /// Pushes the current HEAD to a specific branch on a remote.
    /// </summary>
    public async Task<string> PushToRemoteBranchAsync(
        string remote,
        string branch,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _processRunner.ExecuteAsync(
            GitExecutable,
            $"push {remote} HEAD:{branch}",
            allowNonZeroExitCode: true,
            cancellationToken: cancellationToken
        );
        if (result.ExitCode != 0)
            throw new UserFacingException($"Git push failed: {result.StandardError.Trim()}");

        return string.IsNullOrEmpty(result.StandardOutput) ? result.StandardError.Trim() : result.StandardOutput.Trim();
    }

    /// <summary>
    /// Finds the first remote that is an Azure DevOps URL.
    /// </summary>
    public async Task<string?> GetAzureDevOpsRemoteNameAsync(
        Func<string, bool> isAzureDevOpsUrl,
        CancellationToken cancellationToken = default
    )
    {
        var remotes = await GetRemotesAsync(cancellationToken);
        if (remotes is null)
            return null;

        foreach (var (key, url) in remotes)
        {
            if (key.EndsWith("(push)") && isAzureDevOpsUrl(url))
            {
                // Extract remote name from key like "origin(push)"
                return key[..^6]; // Remove "(push)" suffix
            }
        }

        return null;
    }
}

/// <summary>
/// Represents the status of the Git working tree.
/// </summary>
internal sealed record WorkingTreeStatus(int Staged, int Modified, int Untracked)
{
    public bool IsClean => Staged == 0 && Modified == 0 && Untracked == 0;
}
