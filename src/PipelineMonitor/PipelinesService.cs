// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PipelineMonitor.Authentication;

namespace PipelineMonitor;

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
