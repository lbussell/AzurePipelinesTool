// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PipelineMonitor.Git;

/// <summary>
/// Provides access to Git data from the current working directory.
/// </summary>
internal sealed class GitService(
    IProcessRunner processRunner,
    ILogger<GitService> logger) : IGitRemoteUrlProvider, IGitRepoRootProvider
{
    private const string GitExecutable = "git";
    private const string OriginPushKey = "origin(push)";

    private readonly IProcessRunner _processRunner = processRunner;
    private readonly ILogger<GitService> _logger = logger;
    private Dictionary<string, string>? _cachedRemotes;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>?> GetRemotesAsync(CancellationToken cancellationToken = default)
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
            var result = await _processRunner.ExecuteAsync(GitExecutable, "remote -v", cancellationToken: cancellationToken);

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

    /// <inheritdoc/>
    public async Task<string?> GetRemoteUrlAsync(
        Func<string, bool>? validationFunction = null,
        CancellationToken cancellationToken = default)
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

    /// <inheritdoc/>
    public async Task<string?> GetRepoRootAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner.ExecuteAsync(GitExecutable, "rev-parse --show-toplevel", cancellationToken: cancellationToken);
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
}

internal static class GitServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddGitService()
        {
            services.TryAddProcessRunner();
            services.TryAddSingleton<GitService>();
            services.TryAddSingleton<IGitRemoteUrlProvider>(sp => sp.GetRequiredService<GitService>());
            services.TryAddSingleton<IGitRepoRootProvider>(sp => sp.GetRequiredService<GitService>());
            return services;
        }
    }
}
