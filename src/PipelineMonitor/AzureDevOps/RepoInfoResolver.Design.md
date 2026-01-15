# RepoInfoResolver Design

## Overview

`RepoInfoResolver` is a service that detects Azure DevOps organization, project, and repository information from the current environment. This is a port of the `resolve_instance_project_and_repo` function from the [Azure DevOps CLI extension](https://github.com/Azure/azure-devops-cli-extension/blob/master/azure-devops/azext_devops/dev/common/services.py#L326).

## Source Analysis

The Python implementation follows this resolution flow:

1. **Check command-line arguments**: If organization/project/repo are provided, use them directly.
2. **Auto-detect from Git remote** (if detection enabled):
   - Get current Git remote URL via `git remote -v`
   - Prefer `origin (push)` remote, fall back to other `(push)` remotes
   - Filter remotes to those that look like Azure DevOps URLs (contain `/_git/` or `/_ssh/` or use `ssh://` scheme)
   - Parse Azure DevOps info from the remote URL using `VstsGitUrlInfo`
3. **Fall back to config**: If auto-detect fails, try configuration defaults.

### URL Parsing Logic (`VstsGitUrlInfo`)

The URL parser handles multiple Azure DevOps URL formats:

- **HTTPS (dev.azure.com)**: `https://dev.azure.com/{org}/{project}/_git/{repo}`
- **HTTPS (visualstudio.com)**: `https://{org}.visualstudio.com/{project}/_git/{repo}`
- **SSH (new format)**: `git@ssh.dev.azure.com:v3/{org}/{project}/{repo}`
- **SSH (old format)**: `{org}@vs-ssh.visualstudio.com:v3/{org}/{project}/{repo}`
- **SSH (with _ssh path)**: URLs containing `/_ssh/` path segment

## C# Implementation

### Services

#### 1. `IGitRemoteUrlProvider`

**File**: `GitRemoteUrlProvider.cs`

**Responsibility**: Execute Git commands to discover remote URLs.

```csharp
internal interface IGitRemoteUrlProvider
{
    IReadOnlyDictionary<string, string>? GetRemotes();
    string? GetRemoteUrl(Func<string, bool>? validationFunction = null);
}
```

This service shells out to `git remote -v` following the restriction that we can use `git` directly rather than adding a library like LibGit2Sharp.

#### 2. `IVstsGitUrlParser`

**File**: `VstsGitUrlParser.cs`

**Responsibility**: Parse Azure DevOps Git URLs to extract organization, project, and repository.

```csharp
internal interface IVstsGitUrlParser
{
    bool IsAzureDevOpsUrl(string? url);
    Task<RepoInfo?> ParseAsync(string remoteUrl, CancellationToken cancellationToken = default);
}
```

This service:
- Handles both HTTPS and SSH URL formats
- Converts SSH URLs to HTTPS for processing
- Parses URL components locally
- Validates by calling `GetRepositoryAsync` on the Azure DevOps Git client

**Note**: The Python implementation uses a `get_vsts_info_by_remote_url` API endpoint which is not available in the public .NET SDK (`Microsoft.TeamFoundationServer.Client`). Our implementation parses URLs locally and validates via `GitHttpClient.GetRepositoryAsync` instead.

#### 3. `IRepoInfoResolver`

**File**: `RepoInfoResolver.cs`

**Responsibility**: Main orchestration service that resolves organization, project, and repository.

```csharp
internal interface IRepoInfoResolver
{
    Task<ResolvedRepoInfo> ResolveAsync(
        string? organization = null,
        string? project = null,
        string? repository = null,
        bool detectFromGit = true,
        CancellationToken cancellationToken = default);
}
```

Resolution flow:
1. If all values provided, return them directly
2. If detection enabled, try Git remote URL parsing
3. Return what was resolved (may have null fields)

### Models

**File**: `Model.cs`

```csharp
internal sealed record RepositoryInfo(string Name, Guid? Id = null);

internal sealed record RepoInfo(
    OrganizationInfo Organization,
    ProjectInfo Project,
    RepositoryInfo Repository);
```

**File**: `RepoInfoResolver.cs`

```csharp
internal sealed record ResolvedRepoInfo(
    OrganizationInfo? Organization,
    ProjectInfo? Project,
    RepositoryInfo? Repository);
```

### Key Differences from Python

1. **No config fallback**: We don't implement config file support (can be added later)
2. **No caching**: We skip the remote info cache for simplicity (can be added later)
3. **Structured services**: Instead of module-level functions, use DI-friendly interfaces
4. **Strong typing**: Use record types instead of nullable string tuples
5. **Async by default**: API calls are naturally async in .NET
6. **Use official SDK**: Use `Microsoft.TeamFoundationServer.Client` for Git API calls
7. **Different API for validation**: Use `GetRepositoryAsync` instead of `get_vsts_info_by_remote_url`

### Dependencies

- **Git CLI**: Shell out to `git` for remote URL discovery
- **Microsoft.TeamFoundationServer.Client**: For `GitHttpClient.GetRepositoryAsync`

### URL Format Support

Support the same URL formats as the Python implementation:

| Format | Example |
|--------|---------|
| HTTPS (dev.azure.com) | `https://dev.azure.com/org/project/_git/repo` |
| HTTPS (visualstudio.com) | `https://org.visualstudio.com/project/_git/repo` |
| SSH (new) | `git@ssh.dev.azure.com:v3/org/project/repo` |
| SSH (old) | `org@vs-ssh.visualstudio.com:v3/org/project/repo` |

### Extension Methods Pattern

Following the existing codebase pattern:

```csharp
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
```

### Usage

```csharp
// Register services
builder.Services.TryAddRepoInfoResolver();

// Resolve repo info (auto-detect from git)
var resolver = services.GetRequiredService<IRepoInfoResolver>();
var info = await resolver.ResolveAsync();

// Or with explicit values
var info = await resolver.ResolveAsync(
    organization: "https://dev.azure.com/myorg",
    project: "MyProject",
    repository: "MyRepo",
    detectFromGit: false);
```

## Implementation Files

1. `src/PipelineMonitor/AzureDevOps/GitRemoteUrlProvider.cs` - Git remote discovery
2. `src/PipelineMonitor/AzureDevOps/VstsGitUrlParser.cs` - URL parsing and API validation
3. `src/PipelineMonitor/AzureDevOps/RepoInfoResolver.cs` - Main resolution service
4. `src/PipelineMonitor/Model.cs` - `RepositoryInfo` and `RepoInfo` types
