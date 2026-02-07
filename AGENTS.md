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

AzurePipelinesTool is a .NET CLI tool for discovering and monitoring Azure DevOps pipelines from the command line. It auto-detects Azure DevOps organization/project/repository from local Git remotes.

**CLI Commands** (defined in `Program.cs` as the `App` class):
- `list` (alias: `ls`) - List all local pipelines
- `info <definitionPath>` - Show pipeline info
- `parameters <definitionPath>` - Show pipeline parameters from YAML
- `runs <definitionPath>` - Show recent pipeline runs

**Global tool command:** `azp`

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
├── InteractionService        - Displays errors/warnings/success with Spectre.Console
├── GitService                - Reads Git remote URLs and repository root
├── VstsGitUrlParser          - Parses Azure DevOps Git URLs
└── VssConnectionProvider     - Manages authenticated VSS connections
```

**Key Directories:**
- `src/AzurePipelinesTool/Authentication/` - Azure credential and connection management
- `src/AzurePipelinesTool/AzureDevOps/` - Azure DevOps API integration
- `src/AzurePipelinesTool/AzureDevOps/Yaml/` - Pipeline YAML parsing
- `src/AzurePipelinesTool/Git/` - Git integration via CliWrap

## Testing

Tests use MSTest with parallel execution at method level.

**Test directories:**
- `src/AzurePipelinesTool.Tests/AzureDevOps/Yaml/` - YAML parsing and parameter model tests
- `src/AzurePipelinesTool.Tests/Display/` - Output formatting snapshot tests

**Snapshot testing (Verify):**
Display tests use [Verify](https://github.com/VerifyTests/Verify) for snapshot testing.
Each test captures the full rendered output and compares it against a committed `.verified.txt` file.
DiffEngine is disabled — no diff tools will be launched.

Workflow when output format changes:
1. Run `dotnet test` — changed tests fail, `.received.txt` files are generated
2. Review the `.received.txt` files to confirm the new output looks correct
3. Accept snapshots: copy `.received.txt` to `.verified.txt` (e.g., `Copy-Item *.received.txt *.verified.txt`)
4. Commit the updated `.verified.txt` files

**Display test data (`src/AzurePipelinesTool.Tests/Display/TestData.cs`):**
- Provides realistic sample data based on real command output from the docker-tools-imagebuilder-unofficial pipeline
- `SamplePipelines` — `LocalPipelineInfo` list (from `list` command)
- `SampleVariables` / `SampleParameters` — Pipeline variable and parameter data (from `info` command)
- `SamplePipelineInfoView` — Pre-built Markout view model for the `info` command
- `SucceededTimeline` / `FailedTimeline` / `InProgressTimeline` — `BuildTimelineInfo` with various stage states for `status`/`wait` command testing
- When adding new commands, add corresponding sample data to `TestData.cs` and snapshot tests to the `Display/` directory

## Dependencies

- **Azure.Identity** - Azure authentication (AzureDeveloperCliCredential)
- **Markout** - Structured markdown output (source-generated serializer)
- **Microsoft.TeamFoundationServer.Client** - Azure DevOps API
- **Spectre.Console** - Console abstraction and error/warning formatting
- **ConsoleAppFramework** - CLI framework
- **YamlDotNet** - YAML parsing
- **CliWrap** - Process execution wrapper

## Output Style

- All data output uses the **Markout** library to produce portable markdown (tables, trees, fields).
- Output must be **plain text only** — do not use emoji, Unicode symbols, or special icons.
- Error, warning, and success messages use Spectre.Console markup and are the only exception.
