---
name: update-documentation
description: Update project documentation after code changes. Use when files have been added, modified, or removed and documentation needs to be brought in sync. Identifies what changed and which docs to update.
---

# Update Documentation

Guide for keeping project documentation in sync after code changes.

## Step 1: Identify What Changed

If the user told you specifically what changed, use that information.
If not, figure it out by looking at the git history.

Check for staged/unstaged changes first:

```bash
git diff --name-only
git diff --staged --name-only
```

If there are no local changes, look at the last few commits instead:

```bash
git log --name-only --oneline -5
```

Review the diffs to understand what changed semantically (new command, new flag, version bump, architecture change, etc.).

## Step 2: Update Documentation

Use the table below to determine which documentation files need updating based on the type of change.

### Documentation Map

| Change | Files to Update |
|--------|----------------|
| **New CLI command** | `README.md` (Quick Reference table, Usage examples if needed), `AGENTS.md` (available commands list in Architecture > Commands), `skills/azure-pipelines-tool/SKILL.md` (Command Reference table, Workflows section if applicable, Key Flags table if the command has flags) |
| **New/changed flag on existing command** | `README.md` (Key Flags table), `skills/azure-pipelines-tool/SKILL.md` (Key Flags table, usage examples) |
| **Version bump** | `src/AzurePipelinesTool/AzurePipelinesTool.csproj` (MajorVersion/MinorVersion/PatchVersion), `.claude-plugin/plugin.json` (version field), `.claude-plugin/marketplace.json` (metadata.version and plugins[0].version) — all three must match |
| **New service/layer in architecture** | `AGENTS.md` (Key layers section) |
| **New NuGet dependency** | `AGENTS.md` only if it introduces a new architectural concept |
| **Changed authentication or config** | `AGENTS.md` (Authentication/Configuration sections), `README.md` (Requirements section), `skills/azure-pipelines-tool/SKILL.md` (Requirements section) |
| **Changed error handling patterns** | `AGENTS.md` (Error handling section) |
| **Changed test framework or conventions** | `AGENTS.md` (Tests section), `.github/instructions/csharp.instructions.md` (Tests section if style changed) |
| **Changed build/publish process** | `docs/development/README.md`, `AGENTS.md` (Build and Test Commands section) |
| **Changed code style conventions** | `.github/instructions/csharp.instructions.md`, `AGENTS.md` (Code Style section) |
| **New workflow or CI change** | `docs/development/README.md` (if publishing-related) |

### File Purposes

These are all the documentation files in the project and what each one is responsible for:

- **`README.md`** — User-facing: installation, quick reference command table, usage examples with workflows, key flags table, agent skill installation instructions.
- **`AGENTS.md`** — Developer/agent-facing: project overview, build/test commands, architecture description, error handling patterns, pipeline resolution, configuration, versioning rules, test conventions, code style summary.
- **`CLAUDE.md`** — Entry point for Claude; references AGENTS.md. Rarely needs changes.
- **`skills/azure-pipelines-tool/SKILL.md`** — Agent skill: how to invoke `azp` commands via `dnx`, workflows, key flags, command reference. This file is also embedded as a resource in the built tool.
- **`.claude-plugin/plugin.json`** — Plugin metadata: name, description, version. Version must match csproj.
- **`.claude-plugin/marketplace.json`** — Marketplace metadata: marketplace name, version, plugin list. Version must match csproj.
- **`docs/development/README.md`** — Development guide: prerequisites, build/test, publishing setup, version management, release procedures.
- **`.github/instructions/csharp.instructions.md`** — C# coding style guidelines applied to `**/*.cs` files.
- **`src/AzurePipelinesTool/AzurePipelinesTool.csproj`** — Source of truth for version numbers (MajorVersion, MinorVersion, PatchVersion).

## Step 3: Verify Consistency

After making updates, check that:

1. **Command lists match**: The command list in `README.md`, `AGENTS.md`, `skills/azure-pipelines-tool/SKILL.md`, and `Program.cs` should all list the same user-facing commands.
2. **Versions are in sync**: The version in `AzurePipelinesTool.csproj`, `.claude-plugin/plugin.json`, and `.claude-plugin/marketplace.json` must all match.
3. **Key flags are consistent**: The flags table in `README.md` and `skills/azure-pipelines-tool/SKILL.md` should list the same flags.
