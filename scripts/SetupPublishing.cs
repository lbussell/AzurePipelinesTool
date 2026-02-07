// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

// Automates the GitHub setup steps from the README:
// 1. Creates the 'production' GitHub environment
// 2. Sets the NUGET_USER environment secret
//
// Usage: dotnet run scripts/SetupPublishing.cs

#:package CliWrap@3.10.0
#:package Spectre.Console@0.54.1-alpha.0.31

using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;

AnsiConsole.WriteLine();
var (owner, repo) = await DetectGitHubRepoAsync();
await EnsureGhAuthenticatedAsync();
await CreateEnvironmentAsync(owner, repo);
await SetNugetUserSecretAsync(owner, repo);
await SetupTrustedPublishingAsync(owner, repo);

async Task<(string Owner, string Repo)> DetectGitHubRepoAsync()
{
    var result = await Cli.Wrap("git")
        .WithArguments("remote get-url origin")
        .WithValidation(CommandResultValidation.None)
        .ExecuteBufferedAsync();

    if (result.ExitCode != 0)
    {
        Prompt.Error("Could not detect git remote. Are you in a git repository?");
        Environment.Exit(1);
    }

    var url = result.StandardOutput.Trim();

    // Match HTTPS: https://github.com/{owner}/{repo}.git
    // Match SSH:   git@github.com:{owner}/{repo}.git
    var match = Regex.Match(url, @"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+)");
    if (!match.Success)
    {
        Prompt.Error($"Origin URL is not a GitHub repository: [dim]{url}[/]");
        Environment.Exit(1);
    }

    var detectedOwner = match.Groups["owner"].Value;
    var detectedRepo = match.Groups["repo"].Value;
    AnsiConsole.MarkupLine($"[bold]Detected repository:[/] [link]https://github.com/{detectedOwner}/{detectedRepo}[/]");

    if (!Prompt.Confirm("Is this correct?"))
    {
        AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
        Environment.Exit(1);
    }

    return (detectedOwner, detectedRepo);
}

async Task EnsureGhAuthenticatedAsync()
{
    AnsiConsole.MarkupLine("Checking GitHub CLI authentication...");
    var result = await Cli.Wrap("gh")
        .WithArguments("auth status")
        .WithValidation(CommandResultValidation.None)
        .ExecuteBufferedAsync();

    if (result.ExitCode != 0)
    {
        Prompt.Error("The GitHub CLI is not authenticated. Run [blue]gh auth login[/] first.");
        Environment.Exit(1);
    }
}

static async Task CreateEnvironmentAsync(string owner, string repo)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Creating [green]production[/] GitHub environment[/]");
    await GitHubCli.RunWithConfirmationAsync(
        ["api", "--method", "PUT", $"repos/{owner}/{repo}/environments/production"]
    );
    Prompt.Success("Environment [green]production[/] created.");
}

static async Task SetNugetUserSecretAsync(string owner, string repo)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Set the [green]NUGET_USER[/] environment secret[/]");
    var nugetUser = Prompt.Ask("Enter your [green]NuGet.org username[/]:");

    await GitHubCli.RunWithConfirmationAsync(
        ["secret", "set", "NUGET_USER", "--env", "production", "--repo", $"{owner}/{repo}"],
        stdinText: nugetUser
    );

    Prompt.Success("Secret [green]NUGET_USER[/] set.");
}

static async Task SetupTrustedPublishingAsync(string owner, string repo)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold green]Next step:[/] set up Trusted Publishing on NuGet.org.");
    AnsiConsole.MarkupLine(
        "Go to [link]https://www.nuget.org/account/trustedpublishing[/] and add a new Trusted Publisher with:"
    );
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[bold]Policy Name:[/]      {Markup.Escape(repo)}");
    AnsiConsole.MarkupLine($"[bold]Repository Owner:[/] {Markup.Escape(owner)}");
    AnsiConsole.MarkupLine($"[bold]Repository:[/]       {Markup.Escape(repo)}");
    AnsiConsole.MarkupLine($"[bold]Workflow File:[/]     publish-nuget.yml");
    AnsiConsole.MarkupLine($"[bold]Environment:[/]       production");
}

internal static class Prompt
{
    public static bool Confirm(string message) => AnsiConsole.Confirm(message);

    public static string Ask(string message) => AnsiConsole.Prompt(new TextPrompt<string>(message).PromptStyle("blue"));

    public static void Success(string message) => AnsiConsole.MarkupLine($"[green]✓[/] {message}");

    public static void Skip() => AnsiConsole.MarkupLine("[yellow]Skipped.[/]");

    public static void Error(string message) => AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
}

internal static class GitHubCli
{
    private static readonly Command _gh = Cli.Wrap("gh");

    public static async Task RunWithConfirmationAsync(string[] arguments, string? stdinText = null)
    {
        var command = _gh.WithArguments(arguments).WithValidation(CommandResultValidation.ZeroExitCode);

        var commandString = string.Join(' ', arguments);

        if (stdinText is not null)
            command = command.WithStandardInputPipe(PipeSource.FromString(stdinText));

        if (!Prompt.Confirm($"Run `[blue]gh {commandString}[/]`?"))
            throw new OperationCanceledException("User aborted the operation.");

        var result = await command.ExecuteBufferedAsync(Encoding.UTF8, Encoding.UTF8);

        if (!string.IsNullOrWhiteSpace(result.StandardError))
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(result.StandardError.Trim())}[/]");
    }
}
