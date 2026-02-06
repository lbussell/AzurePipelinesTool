// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using PipelineMonitor.AzureDevOps;

namespace PipelineMonitor.Commands;

internal sealed class CancelCommand(
    InteractionService interactionService,
    PipelinesService pipelinesService,
    BuildIdResolver buildIdResolver
)
{
    private readonly InteractionService _interactionService = interactionService;
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly BuildIdResolver _buildIdResolver = buildIdResolver;

    /// <summary>
    /// Cancel a pipeline run.
    /// </summary>
    /// <param name="buildIdOrUrl">Build ID or Azure DevOps build results URL.</param>
    [Command("cancel")]
    public async Task ExecuteAsync([Argument] string buildIdOrUrl)
    {
        var (org, project, buildId) = await _buildIdResolver.ResolveAsync(buildIdOrUrl);

        try
        {
            await _interactionService.ShowLoadingAsync(
                "Canceling build...",
                async () =>
                {
                    await _pipelinesService.CancelBuildAsync(org, project, buildId);
                    return true;
                }
            );
        }
        catch (Exception ex) when (ex is not UserFacingException)
        {
            throw new UserFacingException($"Failed to cancel build {buildId}: {ex.Message}", ex);
        }

        _interactionService.DisplaySuccess($"Build {buildId} has been canceled.");
    }
}
