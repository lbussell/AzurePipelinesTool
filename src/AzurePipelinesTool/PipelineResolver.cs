// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool.AzureDevOps;

namespace AzurePipelinesTool;

internal sealed class PipelineResolver(PipelinesService pipelinesService)
{
    private readonly PipelinesService _pipelinesService = pipelinesService;

    public async Task<IReadOnlyList<LocalPipelineInfo>> GetLocalPipelinesAsync()
    {
        var pipelinesTask = _pipelinesService.GetLocalPipelinesAsync().ToListAsync().AsTask();

        List<LocalPipelineInfo> pipelines = await pipelinesTask;

        return pipelines;
    }

    public async Task<LocalPipelineInfo> GetLocalPipelineAsync(string definitionPath)
    {
        var pipelines = await GetLocalPipelinesAsync();
        var pipelineFile = new FileInfo(definitionPath);

        if (!pipelineFile.Exists)
            throw new UserFacingException($"Definition file '{definitionPath}' does not exist.");

        var matchingPipelines = pipelines
            .Where(pipeline => pipeline.DefinitionFile.FullName.Equals(pipelineFile.FullName))
            .ToList();

        if (matchingPipelines.Count > 1)
        {
            var names = string.Join(", ", matchingPipelines.Select(p => p.Name));
            throw new UserFacingException(
                $"Multiple pipelines found for '{definitionPath}': {names}. Please disambiguate."
            );
        }

        var pipelineInfo = matchingPipelines.FirstOrDefault();

        if (pipelineInfo is null)
            throw new UserFacingException($"No pipeline found for definition file '{definitionPath}'.");

        return pipelineInfo;
    }
}
