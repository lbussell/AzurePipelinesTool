// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PipelineMonitor;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.Logging;
using Spectre.Console;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

builder.Services.TryAddPipelinesService();
builder.Services.TryAddOrganizationDiscoveryService();
builder.Services.TryAddRepoInfoResolver();
builder.Services.TryAddInteractionService();

builder.Logging.ClearProviders();
builder.Logging.AddFileLogger(builder.Configuration);
builder.Logging.AddLogLocationOnExit();

var host = builder.Build();

var applicationLifetimeTokenSource = new CancellationTokenSource();
var runTask = host.RunAsync(applicationLifetimeTokenSource.Token);

var orgExample = async () =>
{
    var orgService = host.Services.GetRequiredService<IOrganizationDiscoveryService>();
    var organizations = await orgService.GetAccessibleOrganizationsAsync();
    Console.WriteLine("Accessible Organizations:");
    foreach (var org in organizations) Console.WriteLine($"- {org}");
};

var pipelineExample = async () =>
{
    var pipelinesService = host.Services.GetRequiredService<PipelinesService>();
    var pipeline = await pipelinesService.GetPipelineAsync(
        new OrganizationInfo("dnceng", new Uri("https://dev.azure.com/dnceng")),
        new ProjectInfo("internal"),
        new PipelineId(1434));
    Console.WriteLine(pipeline);
};

var allPipelinesExample = async () =>
{
    var pipelinesService = host.Services.GetRequiredService<PipelinesService>();
    var allPipelines = pipelinesService.GetAllPipelinesAsync(
        new OrganizationInfo("dnceng", new Uri("https://dev.azure.com/dnceng")),
        new ProjectInfo("internal"));
    await foreach (var pipeline in allPipelines) Console.WriteLine(pipeline);
};

var repoInfoExample = async () =>
{
    var resolver = host.Services.GetRequiredService<IRepoInfoResolver>();
    var info = await resolver.ResolveAsync();
    Console.WriteLine($"Detected Repository: {info}");
};

var localPipelinesExample = async () =>
{
    var pipelinesService = host.Services.GetRequiredService<PipelinesService>();
    var interactionService = host.Services.GetRequiredService<IInteractionService>();
    var ansiConsole = host.Services.GetRequiredService<IAnsiConsole>();

    var pipelinesTask = pipelinesService.GetLocalPipelinesAsync();
    var pipelines = await interactionService.ShowStatusAsync(
        "Loading pipelines...",
        () => pipelinesTask);

    var table = new Table()
        .Border(TableBorder.Simple)
        .AddColumn("Definition")
        .AddColumn("Pipeline");

    foreach (var pipeline in pipelines)
    {
        table.AddRow($"[blue]{pipeline.RelativePath}[/]", $"[bold green]{pipeline.Name}[/]");
    }

    ansiConsole.Write(new Padder(table));
};

await localPipelinesExample();

// Now stuff is done.
// Signal to stop the application.
applicationLifetimeTokenSource.Cancel();
// And then wait for services to gracefully shut down.
await runTask;
