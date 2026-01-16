// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;

namespace PipelineMonitor.Authentication;

/// <summary>
/// Provides an authenticated connection to Azure DevOps.
/// </summary>
internal interface IVssConnectionProvider
{
    /// <summary>
    /// Gets an authenticated VssConnection for the specified organization.
    /// </summary>
    /// <param name="organization">
    /// URI of the Azure DevOps organization, example: "https://dev.azure.com/orgname"
    /// </param>
    /// <returns></returns>
    VssConnection GetConnection(Uri organization);
}

/// <inheritdoc/>
internal sealed class VssConnectionProvider(IAzureCredentialProvider azureCredentialProvider)
    : IVssConnectionProvider
{
    private readonly IAzureCredentialProvider _azureCredentialProvider = azureCredentialProvider;
    private readonly ConcurrentDictionary<string, Lazy<VssConnection>> _connectionCache = new();

    /// <inheritdoc/>
    public VssConnection GetConnection(Uri organization)
    {
        var cacheKey = organization.AbsoluteUri;
        var lazyConnection = _connectionCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<VssConnection>(() => CreateConnection(organization)));
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

internal static class VssConnectionProviderExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddVssConnectionProvider()
        {
            services.TryAddSingleton<IVssConnectionProvider, VssConnectionProvider>();
            services.TryAddAzureCredentialProvider();
            return services;
        }
    }
}
