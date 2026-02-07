// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Azure.Core;
using Azure.Identity;

namespace AzurePipelinesTool.Authentication;

/// <summary>
/// Provides an Azure credential for authenticating with Azure services
/// using <see cref="AzureDeveloperCliCredential"/>.
/// </summary>
internal sealed class AzureCredentialProvider
{
    private readonly Lazy<TokenCredential> _credential = new(() => new AzureDeveloperCliCredential());

    public TokenCredential GetCredential() => _credential.Value;
}
