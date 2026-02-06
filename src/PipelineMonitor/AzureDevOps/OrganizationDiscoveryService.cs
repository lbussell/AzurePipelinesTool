// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Location.Client;
using Microsoft.VisualStudio.Services.WebApi;
using PipelineMonitor.Authentication;

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Discovers Azure DevOps organizations that the authenticated user has access to.
/// </summary>
internal sealed class OrganizationDiscoveryService(VssConnectionProvider connectionProvider)
{
    // Visual Studio Profile Service (VSPS) endpoint.
    // This is a special Azure DevOps endpoint that is not organization-specific
    // and provides access to user profile and account information across all
    // organizations the user belongs to.
    private static readonly Uri VspsUri = new("https://app.vssps.visualstudio.com");

    private readonly VssConnectionProvider _connectionProvider = connectionProvider;

    /// <summary>
    /// Gets all Azure DevOps organizations the authenticated user has access to.
    /// </summary>
    public async Task<IReadOnlyList<OrganizationInfo>> GetAccessibleOrganizationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var connection = _connectionProvider.GetConnection(VspsUri);

        // Get the authenticated user's member ID using the Location service
        var locationClient = connection.GetClient<LocationHttpClient>();
        var connectionData = await locationClient.GetConnectionDataAsync(
            connectOptions: ConnectOptions.None,
            lastChangeId: -1,
            cancellationToken: cancellationToken
        );
        var memberId = connectionData.AuthenticatedUser.Id;

        // Query for all organizations the user has access to
        var accountClient = connection.GetClient<AccountHttpClient>();
        var accounts = await accountClient.GetAccountsByMemberAsync(
            memberId: memberId,
            cancellationToken: cancellationToken
        );

        return accounts
            .Select(account => new OrganizationInfo(
                Name: account.AccountName,
                Uri: new($"https://dev.azure.com/{account.AccountName}")
            ))
            .ToList();
    }
}
