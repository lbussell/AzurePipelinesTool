// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PipelineMonitor.Authentication;

/// <summary>
/// Provides an Azure credential for authenticating with Azure services.
/// </summary>
internal interface IAzureCredentialProvider
{
    /// <summary>
    /// Gets a credential that can be used to authenticate with Azure services.
    /// </summary>
    TokenCredential GetCredential();
}

/// <summary>
/// Default implementation that uses <see cref="AzureDeveloperCliCredential"/> to
/// authenticate using the Azure Developer CLI (azd).
/// </summary>
internal sealed class AzureCredentialProvider : IAzureCredentialProvider
{
    private readonly Lazy<TokenCredential> _credential = new(() => new AzureDeveloperCliCredential());

    /// <inheritdoc/>
    public TokenCredential GetCredential() => _credential.Value;
}

internal static class AzureCredentialProviderExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddAzureCredentialProvider()
        {
            services.TryAddSingleton<IAzureCredentialProvider, AzureCredentialProvider>();
            return services;
        }
    }
}
