// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool.AzureDevOps;

namespace AzurePipelinesTool;

/// <summary>
/// Resolves a build ID or Azure DevOps build URL into an organization, project, and build ID.
/// </summary>
internal sealed class BuildIdResolver(RepoInfoResolver repoInfoResolver)
{
    private readonly RepoInfoResolver _repoInfoResolver = repoInfoResolver;

    public async Task<(OrganizationInfo Org, ProjectInfo Project, int BuildId)> ResolveAsync(string buildIdOrUrl)
    {
        if (TryParseAzureDevOpsUrl(buildIdOrUrl, out var orgName, out var projectName, out var buildId))
        {
            var org = new OrganizationInfo(orgName, new Uri($"https://dev.azure.com/{orgName}"));
            var project = new ProjectInfo(projectName);
            return (org, project, buildId);
        }

        if (!int.TryParse(buildIdOrUrl, out var id))
            throw new UserFacingException(
                $"Invalid argument '{buildIdOrUrl}'. Provide a numeric build ID or an Azure DevOps build results URL."
            );

        var repoInfo = await _repoInfoResolver.ResolveAsync();
        return repoInfo.Organization is null || repoInfo.Project is null
            ? throw new UserFacingException(
                "Could not detect Azure DevOps organization/project from Git remotes. Use a full build URL instead."
            )
            : (repoInfo.Organization, repoInfo.Project, id);
    }

    /// <summary>
    /// Parses Azure DevOps build URLs in both modern and legacy formats, handling any query parameter
    /// order and URL-encoded org/project names.
    /// </summary>
    private static bool TryParseAzureDevOpsUrl(
        string input,
        out string orgName,
        out string projectName,
        out int buildId
    )
    {
        orgName = "";
        projectName = "";
        buildId = 0;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return false;

        if (
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        )
            return false;

        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return false;

        string? buildIdValue = null;
        foreach (var param in query.TrimStart('?').Split('&'))
        {
            var eqIndex = param.IndexOf('=');
            if (eqIndex <= 0)
                continue;
            if (param[..eqIndex].Equals("buildId", StringComparison.OrdinalIgnoreCase))
            {
                buildIdValue = param[(eqIndex + 1)..];
                break;
            }
        }

        if (buildIdValue is null || !int.TryParse(buildIdValue, out buildId))
            return false;

        var pathParts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Modern: dev.azure.com/{org}/{project}/_build/results
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) && pathParts.Length >= 2)
        {
            orgName = Uri.UnescapeDataString(pathParts[0]);
            projectName = Uri.UnescapeDataString(pathParts[1]);
            return true;
        }

        // Legacy: {org}.visualstudio.com/{project}/_build/results
        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) && pathParts.Length >= 1)
        {
            orgName = uri.Host.Split('.')[0];
            projectName = Uri.UnescapeDataString(pathParts[0]);
            return true;
        }

        return false;
    }
}
