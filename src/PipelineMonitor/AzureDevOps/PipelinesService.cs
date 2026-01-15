// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.Services.WebApi;

using PipelineMonitor.Authentication;

namespace PipelineMonitor.AzureDevOps;

internal sealed class PipelinesService(IVssConnectionProvider vssConnectionProvider)
{
    private readonly IVssConnectionProvider _vssConnectionProvider = vssConnectionProvider;

    public async Task<PipelineInfo> GetPipelineAsync(OrganizationInfo org, ProjectInfo project, PipelineId id)
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();
        var pipeline = await client.GetPipelineAsync(project: project.Name, pipelineId: id.Value);
        var result = new PipelineInfo(pipeline.Name, new PipelineId(pipeline.Id), pipeline.Url, pipeline.Folder);
        return result;
    }

    public async IAsyncEnumerable<PipelineInfo> GetAllPipelinesAsync(OrganizationInfo org, ProjectInfo project)
    {
        var connection = _vssConnectionProvider.GetConnection(org.Uri);
        var client = connection.GetClient<PipelinesHttpClient>();

        string? continuationToken = null;
        do
        {
            List<Pipeline> response = await client.ListPipelinesAsync(project.Name, continuationToken, top: 10);

            foreach (var pipeline in response)
            {
                yield return new PipelineInfo(
                    Name: pipeline.Name,
                    Id: new PipelineId(pipeline.Id),
                    Url: pipeline.Url,
                    Folder: pipeline.Folder);
            }

            if (response is IPagedList pagedListResponse)
            {
                continuationToken = pagedListResponse.ContinuationToken;
            }

        } while (!string.IsNullOrEmpty(continuationToken));
    }
}

internal static class PipelinesServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddPipelinesService()
        {
            services.TryAddSingleton<PipelinesService>();
            services.TryAddVssConnectionProvider();
            return services;
        }
    }
}
