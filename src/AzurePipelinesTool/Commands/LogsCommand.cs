// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool.AzureDevOps;
using ConsoleAppFramework;
using Spectre.Console;

namespace AzurePipelinesTool.Commands;

internal sealed class LogsCommand(
    IAnsiConsole ansiConsole,
    PipelinesService pipelinesService,
    BuildIdResolver buildIdResolver
)
{
    private readonly IAnsiConsole _ansiConsole = ansiConsole;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly BuildIdResolver _buildIdResolver = buildIdResolver;

    /// <summary>
    /// Download logs for a specific task from a pipeline run.
    /// </summary>
    /// <param name="buildIdOrUrl">Build ID or Azure DevOps build results URL.</param>
    /// <param name="logId">Log ID of the task to download logs for. Shown in `status --depth 3` output.</param>
    [Command("logs")]
    public async Task ExecuteAsync(
        [Argument] string buildIdOrUrl,
        [Argument] int logId,
        CancellationToken cancellationToken = default
    )
    {
        var (org, project, buildId) = await _buildIdResolver.ResolveAsync(buildIdOrUrl);

        await using var logStream = await _pipelinesService.GetBuildLogAsync(
            org,
            project,
            buildId,
            logId,
            cancellationToken
        );

        var fileName = $"build-{buildId}-log-{logId}.txt";
        var filePath = Path.Combine(Path.GetTempPath(), fileName);

        await using var fileStream = File.Create(filePath);
        await logStream.CopyToAsync(fileStream, cancellationToken);

        _ansiConsole.WriteLine(filePath);
    }
}
