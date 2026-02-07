# `azp` - Azure Pipelines Tool

A CLI for interacting with Azure Pipelines from the terminal.

## Installation

```bash
dotnet tool install -g azp
```

### Requirements

- [.NET 10+ SDK](https://dotnet.microsoft.com/download)
- Authenticated via [`azd auth login`](https://learn.microsoft.com/azure/developer/azure-developer-cli/reference#azd-auth-login)
- Run from within a Git repo whose remote points to Azure DevOps

## Quick Reference

| Command | Purpose |
| ------- | ------- |
| `azp list` | List all pipelines in the current repository |
| `azp info <path>` | Show pipeline details: variables, parameters, metadata |
| `azp check <path>` | Preview expanded YAML (dry run with template parameters) |
| `azp run <path>` | Queue a pipeline run with parameters, variables, and stage skips |
| `azp status <id>` | Show run status as a tree of stages, jobs, and tasks |
| `azp cancel <id>` | Cancel a running pipeline build |
| `azp wait <id>` | Poll until a run completes, with optional failure exit code |
| `azp logs <id> <logId>` | Download logs for a specific task from a run |

`<path>` is a relative path to the pipeline YAML file.
`<id>` is a numeric build ID or a full Azure DevOps build results URL.

## Usage

Commands chain together in natural workflows.

### Pipeline Development: list → info → check → run → wait

```bash
# Discover pipelines in the current repo
azp list

# Inspect a pipeline's variables and parameters
azp info path/to/pipeline.yml

# Validate YAML expansion with template parameters (dry run, no queue)
azp check path/to/pipeline.yml -p env=staging

# Queue the run (prints build URL and ID)
azp run path/to/pipeline.yml -p env=staging --var imageTag=latest

# Wait for completion using the build ID from the previous step
azp wait 12345 -f
```

`check` and `run` require a clean working tree synced with upstream — commit
and push first.

### Monitoring: status → logs

```bash
# View run status as a tree (stages → jobs → tasks)
azp status 12345 -d 3

# Download logs for a specific task (logId shown in status -d 3 output)
azp logs 12345 42

# Cancel a running build
azp cancel 12345
```

### Key Flags

| Flag | Purpose | Commands |
| ---- | ------- | -------- |
| `-p key=value` | Template parameter override | `check`, `run` |
| `--var key=value` | Pipeline variable override (must be settable at queue time) | `run` |
| `-s`/`--skip` | Stage names to skip | `run` |
| `-d 1\|2\|3` | Tree depth: 1=stages, 2=+jobs (default), 3=+tasks | `status` |
| `-f` | Exit with non-zero code on failure/cancellation | `wait` |

## `azp` Agent Skill

This repo includes a skill which shows AI coding assistants how to use `azp` to
develop Azure Pipelines on your behalf.

With the `azure-pipelines-tool` skill, agents can check pipeline syntax, queue
pipeline runs, investigate failing pipelines, and even wait for runs to complete
and react accordingly.

### Install for GitHub Copilot CLI (Recommended)

```bash
/plugin marketplace add lbussell/AzurePipelinesTool
```

```bash
/plugin install azure-pipelines-tool@lbussell-azure-pipelines-tools
```

## Development

See [docs/development/](docs/development/) for build, test, and publishing
instructions.

## License

[MIT](LICENSE)
