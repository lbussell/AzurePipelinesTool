// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Resolves Azure DevOps organization, project, and repository information from the environment.
/// </summary>
internal interface IRepoInfoResolver
{
    /// <summary>
    /// Resolves Azure DevOps organization, project, and repository from the environment.
    /// Auto-detects from Git remote if values are not provided and detection is enabled.
    /// </summary>
    /// <param name="organization">Optional organization URL or name. If null, attempts to detect.</param>
    /// <param name="project">Optional project name. If null, attempts to detect.</param>
    /// <param name="repository">Optional repository name. If null, attempts to detect.</param>
    /// <param name="detectFromGit">Whether to attempt auto-detection from Git remotes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved information with potentially null fields if resolution failed.</returns>
    Task<ResolvedRepoInfo> ResolveAsync(
        string? organization = null,
        string? project = null,
        string? repository = null,
        bool detectFromGit = true,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc/>
internal sealed partial class RepoInfoResolver(
    IGitRemoteUrlProvider gitRemoteUrlProvider,
    IVstsGitUrlParser vstsGitUrlParser,
    ILogger<RepoInfoResolver> logger) : IRepoInfoResolver
{
    private readonly IGitRemoteUrlProvider _gitRemoteUrlProvider = gitRemoteUrlProvider;
    private readonly IVstsGitUrlParser _vstsGitUrlParser = vstsGitUrlParser;
    private readonly ILogger<RepoInfoResolver> _logger = logger;

    /// <inheritdoc/>
    public async Task<ResolvedRepoInfo> ResolveAsync(
        string? organization = null,
        string? project = null,
        string? repository = null,
        bool detectFromGit = true,
        CancellationToken cancellationToken = default)
    {
        OrganizationInfo? orgInfo = null;
        ProjectInfo? projInfo = null;
        RepositoryInfo? repoInfo = null;

        // If organization is provided, parse it
        if (!string.IsNullOrEmpty(organization))
        {
            orgInfo = ParseOrganization(organization);
        }

        // If project is provided
        if (!string.IsNullOrEmpty(project))
        {
            projInfo = new ProjectInfo(project);
        }

        // If repository is provided
        if (!string.IsNullOrEmpty(repository))
        {
            repoInfo = new RepositoryInfo(repository);
        }

        // If organization is not provided and detection is enabled, try to detect
        if (orgInfo is null && detectFromGit)
        {
            var startTime = DateTime.Now;

            var remoteUrl = _gitRemoteUrlProvider.GetRemoteUrl(_vstsGitUrlParser.IsAzureDevOpsUrl);
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                var detected = await _vstsGitUrlParser.ParseAsync(remoteUrl, cancellationToken);
                if (detected is not null)
                {
                    orgInfo = detected.Organization;

                    // Only use detected project/repo if not already provided
                    if (projInfo is null)
                    {
                        projInfo = detected.Project;
                    }

                    if (repoInfo is null)
                    {
                        repoInfo = detected.Repository;
                    }
                }
            }

            var duration = DateTime.Now - startTime;
            _logger.LogInformation("Detect: URL discovery took {Duration}", duration);
        }

        return new ResolvedRepoInfo(orgInfo, projInfo, repoInfo);
    }

    /// <summary>
    /// Parses an organization string which may be a URL or just a name.
    /// </summary>
    private static OrganizationInfo? ParseOrganization(string organization)
    {
        // Try to parse as URL first
        if (Uri.TryCreate(organization, UriKind.Absolute, out var uri))
        {
            // Extract org name from URL
            if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                var match = VisualStudioComOrgRegex().Match(uri.Host);
                if (match.Success)
                {
                    var orgName = match.Groups["org"].Value;
                    return new OrganizationInfo(orgName, uri);
                }
            }

            if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                var match = DevAzureComPathRegex().Match(uri.AbsolutePath);
                if (match.Success)
                {
                    var orgName = match.Groups["org"].Value;
                    return new OrganizationInfo(orgName, new Uri($"https://dev.azure.com/{orgName}"));
                }
            }

            // Unknown URL format, use as-is
            return new OrganizationInfo(organization, uri);
        }

        // Treat as organization name
        return new OrganizationInfo(organization, new Uri($"https://dev.azure.com/{organization}"));
    }

    // Regex to extract org from visualstudio.com subdomain: {org}.visualstudio.com
    [GeneratedRegex(@"^(?<org>[^\.]+)\.visualstudio\.com", RegexOptions.IgnoreCase)]
    private static partial Regex VisualStudioComOrgRegex();

    // Regex to match dev.azure.com path to extract org: /{org}/...
    [GeneratedRegex(@"^/(?<org>[^/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DevAzureComPathRegex();
}

internal static class RepoInfoResolverExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddRepoInfoResolver()
        {
            services.TryAddGitRemoteUrlProvider();
            services.TryAddVstsGitUrlParser();
            services.TryAddSingleton<IRepoInfoResolver, RepoInfoResolver>();
            return services;
        }
    }
}
