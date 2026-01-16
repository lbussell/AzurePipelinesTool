// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Provides access to Git remote URLs from the current working directory.
/// </summary>
internal interface IGitRemoteUrlProvider
{
    /// <summary>
    /// Gets all Git remote URLs from the current directory.
    /// Returns a dictionary where keys are "{remote_name}({fetch|push})" and values are URLs.
    /// </summary>
    IReadOnlyDictionary<string, string>? GetRemotes();

    /// <summary>
    /// Gets the best remote URL for Azure DevOps detection.
    /// Prefers 'origin (push)', then other '(push)' remotes.
    /// </summary>
    /// <param name="validationFunction">Optional function to filter candidate URLs.</param>
    string? GetRemoteUrl(Func<string, bool>? validationFunction = null);
}

/// <inheritdoc/>
internal sealed class GitRemoteUrlProvider(
    IProcessRunner processRunner,
    ILogger<GitRemoteUrlProvider> logger) : IGitRemoteUrlProvider
{
    private const string GitExecutable = "git";
    private const string OriginPushKey = "origin(push)";

    private readonly IProcessRunner _processRunner = processRunner;
    private readonly ILogger<GitRemoteUrlProvider> _logger = logger;
    private Dictionary<string, string>? _cachedRemotes;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? GetRemotes()
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
            var result = _processRunner.ExecuteAsync(GitExecutable, "remote -v").GetAwaiter().GetResult();

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
    public string? GetRemoteUrl(Func<string, bool>? validationFunction = null)
    {
        var remotes = GetRemotes();
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
}

internal static class GitRemoteUrlProviderExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddGitRemoteUrlProvider()
        {
            services.TryAddProcessRunner();
            services.TryAddSingleton<IGitRemoteUrlProvider, GitRemoteUrlProvider>();
            return services;
        }
    }
}
