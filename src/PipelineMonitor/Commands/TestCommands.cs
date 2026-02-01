// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.Git;

namespace PipelineMonitor.Commands;

internal sealed class TestCommands(
    IInteractionService interactionService,
    GitService gitService,
    IVstsGitUrlParser vstsGitUrlParser)
{
    private readonly IInteractionService _interactionService = interactionService;
    private readonly GitService _gitService = gitService;
    private readonly IVstsGitUrlParser _vstsGitUrlParser = vstsGitUrlParser;

    [Command("hello")]
    public async Task GreetUserAsync(string? name = null)
    {
        name ??= await _interactionService.PromptAsync<string>("What is your name?");
        Console.WriteLine($"Hello, {name}!");
    }

    [Command("confirm")]
    public async Task ConfirmActionAsync()
    {
        var ok = await _interactionService.ConfirmAsync("All good?");
        Console.WriteLine(ok ? "OK!" : "Not OK!");
    }

    [Command("gitstatus")]
    public async Task ShowGitStatusAsync()
    {
        var branch = await _gitService.GetCurrentBranchAsync();
        if (branch is null)
        {
            Console.WriteLine("Not in a git repository");
            return;
        }

        Console.WriteLine($"Branch: {branch}");

        var upstream = await _gitService.GetUpstreamBranchAsync();
        if (upstream is null)
        {
            Console.WriteLine("Upstream: (not set)");
        }
        else
        {
            Console.WriteLine($"Upstream: {upstream}");

            // Get the remote name from upstream (e.g., "origin/main" -> "origin")
            var remoteName = upstream.Split('/')[0];
            var remoteUrl = await _gitService.GetRemoteUrlByNameAsync(remoteName);

            if (remoteUrl is not null)
            {
                Console.WriteLine($"Remote: {remoteUrl}");
                var isAzureDevOps = _vstsGitUrlParser.IsAzureDevOpsUrl(remoteUrl);
                Console.WriteLine($"Azure DevOps: {(isAzureDevOps ? "Yes" : "No")}");
            }

            var aheadBehind = await _gitService.GetAheadBehindAsync();
            if (aheadBehind is not null)
            {
                var (ahead, behind) = aheadBehind.Value;
                if (ahead == 0 && behind == 0)
                    Console.WriteLine("Status: Up to date");
                else
                    Console.WriteLine($"Status: {ahead} ahead, {behind} behind");
            }
        }

        var workingTree = await _gitService.GetWorkingTreeStatusAsync();
        if (workingTree.IsClean)
        {
            Console.WriteLine("Working tree: Clean");
        }
        else
        {
            var parts = new List<string>();
            if (workingTree.Staged > 0) parts.Add($"{workingTree.Staged} staged");
            if (workingTree.Modified > 0) parts.Add($"{workingTree.Modified} modified");
            if (workingTree.Untracked > 0) parts.Add($"{workingTree.Untracked} untracked");
            Console.WriteLine($"Working tree: {string.Join(", ", parts)}");
        }
    }
}
