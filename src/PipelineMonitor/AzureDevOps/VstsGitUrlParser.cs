// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using PipelineMonitor.Authentication;

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Parses Azure DevOps Git URLs to extract organization, project, and repository information.
/// </summary>
internal interface IVstsGitUrlParser
{
    /// <summary>
    /// Determines if a URL appears to be an Azure DevOps Git URL.
    /// </summary>
    bool IsAzureDevOpsUrl(string? url);

    /// <summary>
    /// Parses an Azure DevOps Git URL to extract organization, project, and repo info.
    /// Optionally validates the repository via the Azure DevOps API.
    /// </summary>
    Task<ResolvedRepoInfo?> ParseAsync(string remoteUrl, CancellationToken cancellationToken = default);
}

/// <inheritdoc/>
internal sealed partial class VstsGitUrlParser(
    IVssConnectionProvider connectionProvider,
    ILogger<VstsGitUrlParser> logger) : IVstsGitUrlParser
{
    private readonly IVssConnectionProvider _connectionProvider = connectionProvider;
    private readonly ILogger<VstsGitUrlParser> _logger = logger;

    /// <inheritdoc/>
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
        if (uri.AbsolutePath.Contains("/_git/", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsolutePath.Contains("/_ssh/", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
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
                cancellationToken: cancellationToken);

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

        // dev.azure.com format: https://dev.azure.com/{org}/{project}/_git/{repo}
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var match = DevAzureComUrlRegex().Match(uri.AbsolutePath);
            if (match.Success)
            {
                var orgName = match.Groups["org"].Value;
                var projectName = match.Groups["project"].Value;
                var repoName = match.Groups["repo"].Value;
                var orgUri = new Uri($"https://dev.azure.com/{orgName}");
                return (orgName, projectName, repoName, orgUri);
            }
        }

        // visualstudio.com format: https://{org}.visualstudio.com/{project}/_git/{repo}
        // Also handles: https://{org}.visualstudio.com/DefaultCollection/{project}/_git/{repo}
        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var orgMatch = VisualStudioComOrgRegex().Match(uri.Host);
            if (!orgMatch.Success)
            {
                return null;
            }

            var orgName = orgMatch.Groups["org"].Value;
            var orgUri = new Uri($"https://{uri.Host}");

            var pathMatch = VisualStudioComPathRegex().Match(uri.AbsolutePath);
            if (pathMatch.Success)
            {
                var projectName = pathMatch.Groups["project"].Value;
                var repoName = pathMatch.Groups["repo"].Value;
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

        // Try new SSH URL format: /v3/org/project/repo
        var v3Match = SshV3PathRegex().Match(path);
        if (v3Match.Success)
        {
            var org = v3Match.Groups["org"].Value;
            var project = v3Match.Groups["project"].Value;
            var repo = v3Match.Groups["repo"].Value;

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

            _logger.LogDebug("Unsupported SSH URL format");
            return null;
        }

        // Try old SSH URL format with _ssh path segment
        var httpsUrl = $"https://{netloc}/{path.TrimStart('/')}";
        var sshMatch = SshPathSegmentRegex().Match(httpsUrl);
        if (sshMatch.Success)
        {
            // Replace _ssh with _git
            httpsUrl = SshPathSegmentRegex().Replace(httpsUrl, "_git/");
        }
        
        return httpsUrl;
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

        if (userInfo.Equals("git", StringComparison.OrdinalIgnoreCase) &&
            host.Equals("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase))
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
        if (!url.StartsWith("ssh:", StringComparison.OrdinalIgnoreCase) &&
            (url.Contains("vs-ssh.visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
             url.Contains("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase)))
        {
            // Convert user@host:path format to ssh://user@host/path
            var match = SshShortUrlRegex().Match(url);
            if (match.Success)
            {
                var userHost = match.Groups["userhost"].Value;
                var path = match.Groups["path"].Value;
                url = $"ssh://{userHost}/{path}";
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

    // Regex to match dev.azure.com URL path: /{org}/{project}/_git/{repo}
    [GeneratedRegex(@"^/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DevAzureComUrlRegex();

    // Regex to extract org from visualstudio.com subdomain: {org}.visualstudio.com
    [GeneratedRegex(@"^(?<org>[^\.]+)\.visualstudio\.com", RegexOptions.IgnoreCase)]
    private static partial Regex VisualStudioComOrgRegex();

    // Regex to match visualstudio.com URL path: /{project}/_git/{repo} or /DefaultCollection/{project}/_git/{repo}
    [GeneratedRegex(@"^/(?:DefaultCollection/)?(?<project>[^/]+)/_git/(?<repo>[^/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex VisualStudioComPathRegex();

    // Regex to match SSH v3 path format: /v3/{org}/{project}/{repo}
    [GeneratedRegex(@"^/v3/(?<org>[^/]+)/(?<project>[^/]+)/(?<repo>[^/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SshV3PathRegex();

    // Regex to match _ssh/ path segment (case-insensitive)
    [GeneratedRegex(@"_ssh/", RegexOptions.IgnoreCase)]
    private static partial Regex SshPathSegmentRegex();

    // Regex to match SSH short URL format: user@host:path (without leading slash in path)
    [GeneratedRegex(@"^(?<userhost>[^:]+):(?<path>[^/].*)$")]
    private static partial Regex SshShortUrlRegex();
}

internal static class VstsGitUrlParserExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddVstsGitUrlParser()
        {
            services.TryAddSingleton<IVstsGitUrlParser, VstsGitUrlParser>();
            services.TryAddVssConnectionProvider();
            return services;
        }
    }
}
