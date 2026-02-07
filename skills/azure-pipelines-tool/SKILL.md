---
name: azp
description: Interact with Azure Pipelines from the terminal. Use when you need to list pipelines, inspect definitions, validate YAML, queue runs, monitor status, or download logs. Run from within a Git repo that has Azure DevOps pipelines.
---

# azp

A CLI tool for interacting with Azure Pipelines.

## Requirements

- .NET 10+ SDK
- Authenticated via `azd auth login` (uses Azure Developer CLI credential)
- Run from within a Git repo whose remote points to Azure DevOps

## Installation

Use `dnx` to run without global installation (like `npx` for Node):

```bash
dnx azp -y -- <command>
```

**Important**:

- Always use `-y` to skip the interactive confirmation prompt (which breaks LLM tool use).
- Always use `--` to separate dnx options from tool arguments.

## Workflows

Commands chain together in two natural flows. Each step's output feeds into the next.

### Pipeline Development: list → info → check → run → wait

```bash
# 1. Discover pipelines in the current repo (shows name, ID, YAML path)
dnx azp -y -- list

# 2. Inspect a pipeline's variables and parameters
dnx azp -y -- info path/to/pipeline.yml

# 3. Validate YAML expansion with template parameters (dry run, no queue)
dnx azp -y -- check path/to/pipeline.yml -p env=staging

# 4. Queue the run (prints build URL and ID)
dnx azp -y -- run path/to/pipeline.yml -p env=staging --var imageTag=latest

# 5. Wait for completion using the build ID from step 4
dnx azp -y -- wait 12345 -f
```

`check` and `run` require a clean working tree synced with upstream — commit and push first.

### Monitoring: status → logs

```bash
# 1. View run status as a tree (stages → jobs → tasks)
dnx azp -y -- status 12345 -d 3

# 2. Download logs for a specific task (logId shown in status -d 3 output)
dnx azp -y -- logs 12345 42

# Cancel a running build
dnx azp -y -- cancel 12345
```

`status` and `logs` also accept full Azure DevOps build URLs instead of numeric IDs.

## Key Flags

| Flag | Purpose | Commands |
| ---- | ------- | -------- |
| `-p key=value` | Template parameter override | `check`, `run` |
| `--var key=value` | Pipeline variable override (must be settable at queue time) | `run` |
| `-s`/`--skip` | Stage names to skip | `run` |
| `-d 1\|2\|3` | Tree depth: 1=stages, 2=+jobs (default), 3=+tasks | `status` |
| `-f` | Exit with non-zero code on failure/cancellation | `wait` |

## Command Reference

| Command | Purpose |
| ------- | ------- |
| `list` | List all pipelines associated with the current repository |
| `info <path>` | Show pipeline details: variables, parameters, metadata |
| `check <path>` | Preview expanded YAML (dry run with template parameters) |
| `run <path>` | Queue a pipeline run with parameters, variables, and stage skips |
| `status <id>` | Show run status as a tree of stages, jobs, and tasks |
| `cancel <id>` | Cancel a running pipeline build |
| `wait <id>` | Poll until a run completes, with optional failure exit code |
| `logs <id> <logId>` | Download logs for a specific task from a run |

`<path>` is a relative path to the pipeline YAML file.
`<id>` is a numeric build ID or a full Azure DevOps build results URL.

## When to Use This Skill

- Listing and inspecting Azure Pipelines definitions in a repo
- Validating pipeline YAML before queuing a run
- Queuing pipeline runs with custom parameters and variables
- Monitoring build progress and downloading task logs
- Canceling in-progress pipeline runs
