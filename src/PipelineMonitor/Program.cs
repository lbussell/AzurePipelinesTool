// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PipelineMonitor;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;
using PipelineMonitor.Commands;
using PipelineMonitor.Filters;
using PipelineMonitor.Logging;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

builder.Services.TryAddPipelinesService();
builder.Services.TryAddInteractionService();
builder.Services.TryAddPipelineYamlService();
builder.Services.TryAddPipelineResolver();

builder.Logging.ClearProviders();
builder.Logging.AddFileLogger(builder.Configuration);

#if DEBUG
builder.Logging.AddLogLocationOnExit();
#endif

var consoleAppBuilder = builder.ToConsoleAppBuilder();
consoleAppBuilder.UseFilter<ExceptionHandlingFilter>();
consoleAppBuilder.Add<DiscoverCommand>();
consoleAppBuilder.Add<InfoCommand>();
consoleAppBuilder.Add<ParametersCommand>();
consoleAppBuilder.Add<VariablesCommand>();
consoleAppBuilder.Add<RunsCommand>();

#if DEBUG
consoleAppBuilder.Add<TestCommands>();
#endif

await consoleAppBuilder.RunAsync(args);
