// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();
var host = builder.Build();

var applicationLifetimeTokenSource = new CancellationTokenSource();
var runTask = host.RunAsync(applicationLifetimeTokenSource.Token);

// Do stuff here.

Console.WriteLine("PipelineMonitor");

// Now stuff is done.
// Signal to stop the application.
applicationLifetimeTokenSource.Cancel();
// And then wait for services to gracefully shut down.
await runTask;
