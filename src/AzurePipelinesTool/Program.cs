// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool;
using AzurePipelinesTool.Authentication;
using AzurePipelinesTool.AzureDevOps;
using AzurePipelinesTool.AzureDevOps.Yaml;
using AzurePipelinesTool.Commands;
using AzurePipelinesTool.Display;
using AzurePipelinesTool.Filters;
using AzurePipelinesTool.Git;
using AzurePipelinesTool.Logging;
using ConsoleAppFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Spectre.Console;

var builder = Host.CreateApplicationBuilder();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
builder.AddXdgAppConfiguration(appNameDirectory: nameof(AzurePipelinesTool));

builder.Services.TryAddSingleton<IProcessRunner, CliWrapProcessRunner>();
builder.Services.TryAddSingleton<IEnvironment, SystemEnvironment>();
builder.Services.TryAddSingleton<GitService>();
builder.Services.TryAddSingleton<AzureCredentialProvider>();
builder.Services.TryAddSingleton<VssConnectionProvider>();
builder.Services.TryAddSingleton<VstsGitUrlParser>();
builder.Services.TryAddSingleton<RepoInfoResolver>();
builder.Services.TryAddSingleton<OrganizationDiscoveryService>();
builder.Services.TryAddSingleton<PipelinesService>();
builder.Services.TryAddSingleton(_ => AnsiConsole.Console);
builder.Services.TryAddSingleton<InteractionService>();
builder.Services.TryAddSingleton<PipelineYamlService>();
builder.Services.TryAddSingleton<PipelineResolver>();
builder.Services.TryAddSingleton<BuildIdResolver>();

// Add file logging
builder.Logging.ClearProviders();
builder.Logging.AddNLog(builder.Configuration);

// Print log file location during development
#if DEBUG
builder.Services.AddHostedService<LogLocationService>();
#endif

var consoleAppBuilder = builder.ToConsoleAppBuilder();
consoleAppBuilder.UseFilter<ExceptionHandlingFilter>();

consoleAppBuilder.Add<ListCommand>();
consoleAppBuilder.Add<InfoCommand>();
consoleAppBuilder.Add<RunCommand>();
consoleAppBuilder.Add<StatusCommand>();
consoleAppBuilder.Add<CancelCommand>();
consoleAppBuilder.Add<WaitCommand>();
consoleAppBuilder.Add<LogsCommand>();

await consoleAppBuilder.RunAsync(args);
