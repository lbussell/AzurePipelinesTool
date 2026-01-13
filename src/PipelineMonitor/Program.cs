// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PipelineMonitor;
using PipelineMonitor.AzureDevOps;

var builder = Host.CreateApplicationBuilder();
builder.Services.TryAddPipelinesService();
builder.Services.TryAddOrganizationDiscoveryService();
var host = builder.Build();

var applicationLifetimeTokenSource = new CancellationTokenSource();
var runTask = host.RunAsync(applicationLifetimeTokenSource.Token);

var runOrgExample = async () =>
{
    var orgService = host.Services.GetRequiredService<IOrganizationDiscoveryService>();
    var organizations = await orgService.GetAccessibleOrganizationsAsync();
    Console.WriteLine("Accessible Organizations:");
    foreach (var org in organizations) Console.WriteLine($"- {org}");
};

var runPipelineExample = async () =>
{
    var pipelinesService = host.Services.GetRequiredService<PipelinesService>();
    var pipeline = await pipelinesService.GetPipelineAsync(
        new OrganizationInfo("dnceng", new Uri("https://dev.azure.com/dnceng")),
        new ProjectInfo("internal"),
        new PipelineId(1434));
    Console.WriteLine(pipeline);
};

// Now stuff is done.
// Signal to stop the application.
applicationLifetimeTokenSource.Cancel();
// And then wait for services to gracefully shut down.
await runTask;
