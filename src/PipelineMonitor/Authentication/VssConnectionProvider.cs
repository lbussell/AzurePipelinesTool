// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using Azure.Core;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;

namespace PipelineMonitor.Authentication;

/// <summary>
/// Provides authenticated connections to Azure DevOps organizations.
/// </summary>
internal sealed class VssConnectionProvider(AzureCredentialProvider azureCredentialProvider)
{
    private readonly AzureCredentialProvider _azureCredentialProvider = azureCredentialProvider;
    private readonly ConcurrentDictionary<string, Lazy<VssConnection>> _connectionCache = new();

    public VssConnection GetConnection(Uri organization)
    {
        var cacheKey = organization.AbsoluteUri;
        var lazyConnection = _connectionCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<VssConnection>(() => CreateConnection(organization))
        );
        return lazyConnection.Value;
    }

    private VssConnection CreateConnection(Uri organization)
    {
        // This scope provides access to Azure DevOps Services REST API.
        // See https://learn.microsoft.com/rest/api/azure/devops/tokens/?view=azure-devops-rest-7.1&tabs=powershell#personal-access-tokens-pats
        const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

        var credential = _azureCredentialProvider.GetCredential();
        var requestContext = new TokenRequestContext([AzureDevOpsScope]);
        var tokenObject = credential.GetToken(requestContext, CancellationToken.None);
        var tokenString = tokenObject.Token;

        var vssCredential = new VssOAuthAccessTokenCredential(tokenString);
        var vssConnection = new VssConnection(baseUrl: organization, credentials: vssCredential);
        return vssConnection;
    }
}
