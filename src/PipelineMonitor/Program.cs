// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PipelineMonitor;

var builder = Host.CreateApplicationBuilder();
builder.Services.TryAddPipelinesService();
var host = builder.Build();

var applicationLifetimeTokenSource = new CancellationTokenSource();
var runTask = host.RunAsync(applicationLifetimeTokenSource.Token);

var pipelinesService = host.Services.GetRequiredService<PipelinesService>();
var pipeline = await pipelinesService.GetPipelineAsync(
    new OrganizationInfo("dnceng", new Uri("https://dev.azure.com/dnceng")),
    new ProjectInfo("internal"),
    new PipelineId(1434));

Console.WriteLine(pipeline);
Console.WriteLine("Done");

// Now stuff is done.
// Signal to stop the application.
applicationLifetimeTokenSource.Cancel();
// And then wait for services to gracefully shut down.
await runTask;
