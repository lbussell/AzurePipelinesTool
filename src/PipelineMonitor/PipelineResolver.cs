// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using PipelineMonitor.AzureDevOps;

namespace PipelineMonitor;

internal sealed class PipelineResolver(
    PipelinesService pipelinesService,
    InteractionService interactionService)
{
    private readonly PipelinesService _pipelinesService = pipelinesService;
    private readonly InteractionService _interactionService = interactionService;

    public async Task<IReadOnlyList<LocalPipelineInfo>> GetLocalPipelinesAsync()
    {
        var pipelinesTask = _pipelinesService
            .GetLocalPipelinesAsync()
            .ToListAsync()
            .AsTask();

        List<LocalPipelineInfo> pipelines = await _interactionService
            .ShowLoadingAsync("Loading Pipelines...", () => pipelinesTask);

        return pipelines;
    }

    public async Task<LocalPipelineInfo> GetLocalPipelineAsync(string definitionPath)
    {
        var pipelines = await GetLocalPipelinesAsync();
        var pipelineFile = new FileInfo(definitionPath);

        if (!pipelineFile.Exists)
            throw new UserFacingException($"Definition file '{definitionPath}' does not exist.");

        var matchingPipelines = pipelines
            .Where(pipeline =>
                pipeline.DefinitionFile.FullName.Equals(pipelineFile.FullName))
            .ToList();

        var pipelineInfo = matchingPipelines.FirstOrDefault();

        if (matchingPipelines.Count > 1)
            pipelineInfo = await _interactionService.SelectAsync(
                "Multiple pipelines found for the specified definition file. Please select one:",
                matchingPipelines,
                pipeline => pipeline.Name);

        if (pipelineInfo is null)
            throw new UserFacingException($"No pipeline found for definition file '{definitionPath}'.");

        return pipelineInfo;
    }
}


