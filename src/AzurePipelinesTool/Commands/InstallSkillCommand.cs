// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Reflection;
using ConsoleAppFramework;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

internal sealed class InstallSkillCommand(IAnsiConsole ansiConsole)
{
    private const string SkillFileName = "SKILL.md";
    private const string SkillDirectoryName = "azure-pipelines-tool";

    private static readonly Location CustomLocation = new("Custom", "");

    private static readonly Location[] Locations =
    [
        new("Here", Environment.CurrentDirectory),
        new("User Directory", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
    ];

    private static readonly Agent[] Agents =
    [
        new("GitHub Copilot", LocalPrefix: ".github", UserPrefix: ".copilot"),
        new("Claude Code", LocalPrefix: ".claude", UserPrefix: ".claude"),
        new("Gemini", LocalPrefix: ".gemini", UserPrefix: ".gemini"),
        new("Agent-agnostic", LocalPrefix: ".agents", UserPrefix: ".agents"),
    ];

    private readonly IAnsiConsole _ansiConsole = ansiConsole;

    /// <summary>
    /// Install the agent skill file to a local or user directory.
    /// </summary>
    [Command("install-skill")]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var skillContent = ReadEmbeddedSkill();
        var targetDir = await ResolveTargetDirectoryAsync(cancellationToken);
        var targetFile = Path.Combine(targetDir, SkillFileName);

        await EnsureDirectoryExistsAsync(targetDir, cancellationToken);
        var wroteFile = await WriteFileWithConfirmationAsync(targetFile, skillContent, cancellationToken);
        if (!wroteFile)
        {
            _ansiConsole.MarkupLine("[yellow]Installation canceled.[/]");
            return;
        }

        _ansiConsole.MarkupLine($"[green]Success![/] Wrote [blue]{targetFile}[/]");
    }

    private async Task<bool> WriteFileWithConfirmationAsync(string targetFile, string skillContent, CancellationToken cancellationToken)
    {
        var fileExists = File.Exists(targetFile);
        if (fileExists)
        {
            _ansiConsole.MarkupLineInterpolated($"[yellow]Warning:[/] File [blue]{targetFile}[/] already exists.");
            var okToOverwrite = await ConfirmAsync("Overwrite it?", cancellationToken);
            if (!okToOverwrite)
            {
                return false;
            }
        }

        await File.WriteAllTextAsync(targetFile, skillContent, cancellationToken);
        return true;
    }

    private async Task<string> ResolveTargetDirectoryAsync(CancellationToken cancellationToken)
    {
        var locationPrompt = "Where do you want to install the skill?";
        var location = await _ansiConsole.PromptAsync(
            new SelectionPrompt<Location>()
                .Title(locationPrompt)
                .UseConverter(l => l == CustomLocation ? l.Name : $"{l.Name} [dim]({l.BaseDir})[/]")
                .AddChoices([..Locations, CustomLocation]),
            cancellationToken);

        if (location == CustomLocation)
        {
            var prompt = new TextPrompt<string>(locationPrompt);
            var customDir = await _ansiConsole.PromptAsync(prompt, cancellationToken);
            var result = Path.Combine(customDir, SkillDirectoryName);
            _ansiConsole.MarkupLineInterpolated($"Installing to {result}[/]");
            return result;
        }

        _ansiConsole.MarkupLineInterpolated($"{locationPrompt} [blue]{location.BaseDir}[/]");

        var agentChoices = Agents
            .Select(a => new AgentChoice(a, location))
            .ToArray();

        var selectedAgent = await _ansiConsole.PromptAsync(
            new SelectionPrompt<AgentChoice>()
                .Title("Which agent skill directory?")
                .UseConverter(c => $"{c.Agent.Name} [dim]({Markup.Escape(c.SkillsDir)})[/]")
                .AddChoices(agentChoices),
            cancellationToken);

        _ansiConsole.MarkupLineInterpolated($"Skill will be installed to [blue]{selectedAgent.FullPath}[/]");
        return selectedAgent.FullPath;
    }

    private async Task EnsureDirectoryExistsAsync(string directory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(directory))
        {
            _ansiConsole.MarkupLineInterpolated($"Directory [blue]{directory.EscapeMarkup()}[/] already exists.");
            return;
        }

        _ansiConsole.MarkupLineInterpolated($"Creating directory [blue]{directory}[/]");
        Directory.CreateDirectory(directory);
    }

    private async Task<bool> ConfirmAsync(string markupPrompt, CancellationToken cancellationToken) =>
        await _ansiConsole.PromptAsync(new ConfirmationPrompt(markupPrompt), cancellationToken);

    private static string ReadEmbeddedSkill()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SkillFileName)
            ?? throw new UserFacingException("Embedded SKILL.md resource not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record Location(string Name, string BaseDir);

    private sealed record Agent(string Name, string LocalPrefix, string UserPrefix);

    private sealed record AgentChoice(Agent Agent, Location Location)
    {
        private bool IsLocal => Location == Locations[0];
        public string SkillsDir => Path.Combine(IsLocal ? Agent.LocalPrefix : Agent.UserPrefix, "skills");
        public string FullPath => Path.Combine(Location.BaseDir, SkillsDir, SkillDirectoryName);
    }
}
