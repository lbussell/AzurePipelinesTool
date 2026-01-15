// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Text.RegularExpressions;
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
internal sealed partial class GitRemoteUrlProvider(ILogger<GitRemoteUrlProvider> logger) : IGitRemoteUrlProvider
{
    private const string GitExecutable = "git";
    private const string OriginPushKey = "origin(push)";

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
            var startInfo = new ProcessStartInfo
            {
                FileName = GitExecutable,
                Arguments = "remote -v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogDebug("Failed to start git process");
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogDebug("git remote -v exited with code {ExitCode}", process.ExitCode);
                return null;
            }

            _cachedRemotes = [];

            // Parse each line using regex: {name}\s+{url}\s+({fetch|push})
            foreach (var match in GitRemoteLineRegex().Matches(output).Cast<Match>())
            {
                var name = match.Groups["name"].Value;
                var url = match.Groups["url"].Value;
                var type = match.Groups["type"].Value;
                
                // Key format: "{name}({fetch|push})" matching Python implementation
                var key = name + type;
                _cachedRemotes[key] = url;
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

    // Regex to parse git remote -v output: {name}\s+{url}\s+({fetch|push})
    [GeneratedRegex(@"^(?<name>\S+)\s+(?<url>\S+)\s+(?<type>\([^)]+\))", RegexOptions.Multiline)]
    private static partial Regex GitRemoteLineRegex();
}

internal static class GitRemoteUrlProviderExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddGitRemoteUrlProvider()
        {
            services.TryAddSingleton<IGitRemoteUrlProvider, GitRemoteUrlProvider>();
            return services;
        }
    }
}
