// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using AzurePipelinesTool.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AzurePipelinesTool.AzureDevOps;

/// <summary>
/// Parses Azure DevOps Git URLs to extract organization, project, and repository information.
/// </summary>
internal sealed partial class VstsGitUrlParser(
    VssConnectionProvider connectionProvider,
    ILogger<VstsGitUrlParser> logger
)
{
    private readonly VssConnectionProvider _connectionProvider = connectionProvider;
    private readonly ILogger<VstsGitUrlParser> _logger = logger;

    public bool IsAzureDevOpsUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        var uri = ParseUri(url);
        if (uri is null)
        {
            return false;
        }

        // Exclude GitHub
        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for Azure DevOps path patterns or SSH scheme
        if (
            uri.AbsolutePath.Contains("/_git/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("/_ssh/", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return false;
    }

    public async Task<ResolvedRepoInfo?> ParseAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Parsing remote URL: {RemoteUrl}", remoteUrl);

        // Convert SSH URL to HTTPS for parsing
        var httpsUrl = ConvertToHttpsUrl(remoteUrl);
        if (httpsUrl is null)
        {
            _logger.LogDebug("Could not convert URL to HTTPS format");
            return null;
        }

        _logger.LogDebug("Using HTTPS URL: {HttpsUrl}", httpsUrl);

        // Parse the URL locally to extract org/project/repo
        var parsed = ParseHttpsUrl(httpsUrl);
        if (parsed is null)
        {
            _logger.LogDebug("Could not parse URL components");
            return null;
        }

        var (orgName, projectName, repoName, orgUri) = parsed.Value;

        try
        {
            // Validate by calling the Azure DevOps API to get the repository
            var connection = _connectionProvider.GetConnection(orgUri);
            var gitClient = connection.GetClient<GitHttpClient>();

            var repository = await gitClient.GetRepositoryAsync(
                project: projectName,
                repositoryId: repoName,
                cancellationToken: cancellationToken
            );

            if (repository is null)
            {
                _logger.LogDebug("Repository not found via API");
                return null;
            }

            var organization = new OrganizationInfo(orgName, orgUri);
            var project = new ProjectInfo(repository.ProjectReference?.Name ?? projectName);
            var repoInfo = new RepositoryInfo(repository.Name, repository.Id);

            return new ResolvedRepoInfo(organization, project, repoInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Auto-detect from git remote URL failed: {Message}", ex.Message);
            _logger.LogDebug(ex, "Exception details");
            return null;
        }
    }

    /// <summary>
    /// Parses an HTTPS Azure DevOps URL to extract org, project, repo, and org URI.
    /// </summary>
    private static (string OrgName, string ProjectName, string RepoName, Uri OrgUri)? ParseHttpsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var pathParts = uri.AbsolutePath.Trim('/').Split('/');

        // dev.azure.com format: https://dev.azure.com/{org}/{project}/_git/{repo}
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            // Path: org/project/_git/repo
            if (pathParts.Length >= 4 && pathParts[2].Equals("_git", StringComparison.OrdinalIgnoreCase))
            {
                var orgName = pathParts[0];
                var projectName = pathParts[1];
                var repoName = pathParts[3];
                var orgUri = new Uri($"https://dev.azure.com/{orgName}");
                return (orgName, projectName, repoName, orgUri);
            }
        }

        // visualstudio.com format: https://{org}.visualstudio.com/{project}/_git/{repo}
        // Also handles: https://{org}.visualstudio.com/DefaultCollection/{project}/_git/{repo}
        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var orgName = uri.Host.Split('.')[0];
            var orgUri = new Uri($"https://{uri.Host}");

            // Find _git index
            var gitIndex = Array.FindIndex(pathParts, p => p.Equals("_git", StringComparison.OrdinalIgnoreCase));

            if (gitIndex >= 1 && gitIndex < pathParts.Length - 1)
            {
                // Project is the segment before _git (skip DefaultCollection if present)
                var projectIndex = gitIndex - 1;
                if (
                    pathParts[projectIndex].Equals("DefaultCollection", StringComparison.OrdinalIgnoreCase)
                    && projectIndex > 0
                )
                {
                    projectIndex--;
                }

                // Actually for DefaultCollection format, project comes after DefaultCollection
                // Let me reconsider: DefaultCollection/project/_git/repo
                // So _git is at index 2, project at index 1
                var projectName = pathParts[gitIndex - 1];
                if (projectName.Equals("DefaultCollection", StringComparison.OrdinalIgnoreCase) && gitIndex >= 2)
                {
                    projectName = pathParts[gitIndex - 1]; // This is still DefaultCollection, need to handle differently
                }

                // Simpler approach: project is immediately before _git
                projectName = pathParts[gitIndex - 1];
                var repoName = pathParts[gitIndex + 1];

                return (orgName, projectName, repoName, orgUri);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts various Azure DevOps URL formats to HTTPS.
    /// </summary>
    private string? ConvertToHttpsUrl(string url)
    {
        var uri = ParseUri(url);
        if (uri is null)
        {
            return null;
        }

        // Already HTTPS
        if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // Handle SSH URLs
        if (uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertSshToHttps(uri, url);
        }

        return url;
    }

    /// <summary>
    /// Converts SSH URL to HTTPS format.
    /// </summary>
    private string? ConvertSshToHttps(Uri uri, string originalUrl)
    {
        var netloc = ConvertSshNetlocToHttpsNetloc(uri.Host, uri.UserInfo);
        if (netloc is null)
        {
            return null;
        }

        var path = uri.AbsolutePath;
        const string SshPathSegment = "_ssh/";
        var sshIndex = path.IndexOf(SshPathSegment, StringComparison.OrdinalIgnoreCase);

        if (sshIndex < 0)
        {
            // New SSH URL format: /v3/org/project/repo
            var pathParts = path.Trim('/').Split('/');

            // Expected format: v3/org/project/repo
            if (pathParts.Length >= 4 && pathParts[0].Equals("v3", StringComparison.OrdinalIgnoreCase))
            {
                var org = pathParts[1];
                var project = pathParts[2];
                var repo = pathParts[3];

                if (netloc.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase))
                {
                    // visualstudio.com format: https://org.visualstudio.com/project/_git/repo
                    return $"https://{org}.visualstudio.com/{project}/_git/{repo}";
                }
                else if (netloc.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase))
                {
                    // dev.azure.com format: https://dev.azure.com/org/project/_git/repo
                    return $"https://dev.azure.com/{org}/{project}/_git/{repo}";
                }
            }

            _logger.LogDebug("Unsupported SSH URL format");
            return null;
        }
        else
        {
            // Old SSH URL format with _ssh path segment
            // Replace _ssh with _git
            var httpsUrl = $"https://{netloc}/{path.TrimStart('/')}";
            sshIndex = httpsUrl.IndexOf(SshPathSegment, StringComparison.OrdinalIgnoreCase);
            if (sshIndex >= 0)
            {
                httpsUrl = httpsUrl[..sshIndex] + "_git/" + httpsUrl[(sshIndex + SshPathSegment.Length)..];
            }
            return httpsUrl;
        }
    }

    /// <summary>
    /// Converts SSH netloc (host) to HTTPS netloc.
    /// </summary>
    private string? ConvertSshNetlocToHttpsNetloc(string host, string? userInfo)
    {
        // Handle hosted URLs with @ (e.g., git@ssh.dev.azure.com)
        if (string.IsNullOrEmpty(userInfo))
        {
            // On-premise URL - not supported yet
            _logger.LogWarning("Azure DevOps SSH URLs without user@ prefix are not supported for repo auto-detection");
            return null;
        }

        // Match pattern like: git@ssh.dev.azure.com or org@vs-ssh.visualstudio.com
        // For git@ssh.dev.azure.com -> return dev.azure.com
        // For org@vs-ssh.visualstudio.com -> return org.visualstudio.com

        if (
            userInfo.Equals("git", StringComparison.OrdinalIgnoreCase)
            && host.Equals("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            return "dev.azure.com";
        }

        // Match: {user}@vs-ssh.visualstudio.com -> {user}.visualstudio.com
        var match = SshNetlocRegex().Match($"{userInfo}@{host}");
        if (match.Success)
        {
            var user = match.Groups[1].Value;
            var domain = match.Groups[2].Value;
            return $"{user}{domain}";
        }

        return null;
    }

    /// <summary>
    /// Parses a URL, handling special SSH URL formats.
    /// </summary>
    private static Uri? ParseUri(string url)
    {
        // Handle new SSH URLs that don't start with ssh:// but contain SSH hosts
        // e.g., git@ssh.dev.azure.com:v3/org/project/repo
        // e.g., org@vs-ssh.visualstudio.com:v3/org/project/repo
        if (
            !url.StartsWith("ssh:", StringComparison.OrdinalIgnoreCase)
            && (
                url.Contains("vs-ssh.visualstudio.com", StringComparison.OrdinalIgnoreCase)
                || url.Contains("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            // Convert user@host:path format to ssh://user@host/path
            var colonIndex = url.IndexOf(':');
            if (colonIndex > 0 && !url[..colonIndex].Contains('/'))
            {
                url = "ssh://" + url[..colonIndex] + "/" + url[(colonIndex + 1)..];
            }
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        return null;
    }

    // Regex to match SSH netloc: user@vs-ssh.domain
    [GeneratedRegex(@"([^@]+)@[^\.]+(\.[^:]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SshNetlocRegex();
}
