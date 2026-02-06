# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Build Commands

```bash
dotnet build                 # Build the project
dotnet test                  # Run all tests
dotnet pack                  # Create NuGet package
```

Run a single test:
```bash
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

## Project Overview

PipelineMonitor is a .NET CLI tool for discovering and monitoring Azure DevOps pipelines from the command line. It auto-detects Azure DevOps organization/project/repository from local Git remotes.

**CLI Commands** (defined in `Program.cs` as the `App` class):
- `list` (alias: `ls`) - List all local pipelines
- `info <definitionPath>` - Show pipeline info
- `parameters <definitionPath>` - Show pipeline parameters from YAML
- `runs <definitionPath>` - Show recent pipeline runs

**Global tool command:** `pipelinemon`

## Architecture

```
Program.cs (Entry Point & CLI Commands)
    ↓
Dependency Injection (ConsoleAppFramework + Microsoft.Extensions.Hosting + Microsoft.Extensions.DependencyInjection)
    ↓
Service Layer:
├── PipelinesService          - Fetches pipeline data from Azure DevOps API
├── PipelineYamlService       - Parses Azure Pipelines YAML files
├── RepoInfoResolver          - Detects org/project/repo from Git remotes
├── InteractionService        - Displays output with Spectre.Console
├── GitService                - Reads Git remote URLs and repository root
├── VstsGitUrlParser          - Parses Azure DevOps Git URLs
└── VssConnectionProvider     - Manages authenticated VSS connections
```

**Key Directories:**
- `src/PipelineMonitor/Authentication/` - Azure credential and connection management
- `src/PipelineMonitor/AzureDevOps/` - Azure DevOps API integration
- `src/PipelineMonitor/AzureDevOps/Yaml/` - Pipeline YAML parsing
- `src/PipelineMonitor/Git/` - Git integration via CliWrap

## Testing

Tests are in`src/PipelineMonitor.Tests/AzureDevOps/Yaml/` and use MSTest with parallel execution at method level.

## Dependencies

- **Azure.Identity** - Azure authentication (AzureDeveloperCliCredential)
- **Microsoft.TeamFoundationServer.Client** - Azure DevOps API
- **Spectre.Console** - Rich console output/tables
- **ConsoleAppFramework** - CLI framework
- **YamlDotNet** - YAML parsing
- **CliWrap** - Process execution wrapper
