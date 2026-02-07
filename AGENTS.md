# AGENTS.md

This file provides guidance for working with code in this repository.

## Project Overview

AzurePipelinesTool (`azp`) is a .NET CLI tool for interacting with Azure Pipelines from the terminal. It is packaged as a .NET global tool (`dotnet tool install`) and published to NuGet as `LoganBussell.AzurePipelinesTool`.

## Build and Test Commands

```bash
dotnet build       # Build all projects
dotnet test        # Run all tests

# Run a single test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Run the tool locally
dotnet run --project src/AzurePipelinesTool -- <command> [args]
```

## Architecture

**Framework**: .NET 10, C# 14. Uses [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework) for CLI command routing and Microsoft.Extensions.Hosting for DI.

**Authentication**: Uses `AzureDeveloperCliCredential` (Azure Identity) — users must be logged in via `azd auth login`. Connections to Azure DevOps are managed through `VssConnectionProvider`.

**Key layers**:

- **Commands** (`Commands/`): Each CLI command is a class with a `[Command]` attribute. Commands are registered in `Program.cs`. Available commands: `list`, `info`, `run`, `check`, `status`, `cancel`, `wait`, `logs`.
- **AzureDevOps** (`AzureDevOps/`): Service layer wrapping the Azure DevOps SDK (`Microsoft.TeamFoundationServer.Client`). `PipelinesService` is the main entry point for all API calls. `RepoInfoResolver` detects org/project/repo from git remotes.
- **Git** (`Git/`): `GitService` wraps git CLI calls via `IProcessRunner`/`CliWrap`. Used to detect the current repo, branch, remote URLs, and working tree status.
- **Display** (`Display/`): Console output using Spectre.Console and [Markout](https://github.com/lbussell/Markout) for structured rendering (tables, trees).
- **Model.cs**: Shared domain records (`OrganizationInfo`, `PipelineInfo`, `LocalPipelineInfo`, `PipelineRunInfo`, etc.).

**Error handling**: Throw `UserFacingException` for expected errors that should be shown to users without a stack trace. The `ExceptionHandlingFilter` catches these and displays a clean message.

**Pipeline resolution**: Commands that operate on pipelines take a YAML definition file path. `PipelineResolver` maps local YAML files to Azure DevOps pipeline definitions. Commands that operate on runs accept either a numeric build ID or a full Azure DevOps build URL.

**Configuration**: XDG-compliant config stored at `~/.config/AzurePipelinesTool/config.json`. Build settings shared via `src/Directory.Build.props` and `src/Directory.Packages.props` (central package management).

## Tests

Uses MSTest with [Verify](https://github.com/VerifyTests/Verify) for snapshot testing. Verified snapshots are `.verified.txt` files alongside test classes. Tests access internal members via `InternalsVisibleTo`.

## Code Style

See `.github/instructions/csharp.instructions.md` and `.editorconfig`. Key points:

- Use latest C# 14 features, file-scoped namespaces, collection expressions
- New classes/records should be `internal sealed`
- Use `var` when type is apparent, LINQ over loops, pattern matching
- Use `is null` / `is not null` (never `== null`)
- Immutable records for DTOs
- No "Arrange, Act, Assert" comments in tests
