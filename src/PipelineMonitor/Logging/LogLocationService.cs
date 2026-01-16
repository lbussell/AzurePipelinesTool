// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Targets;

namespace PipelineMonitor.Logging;

internal sealed class LogLocationService(IInteractionService interactionService) : IHostedLifecycleService
{
    private readonly IInteractionService _interactionService = interactionService;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        var fileTarget = LogManager.Configuration?.FindTargetByName<FileTarget>("logfile");
        if (fileTarget is not null)
        {
            var logEventInfo = new LogEventInfo { TimeStamp = DateTime.Now };
            var fileName = fileTarget.FileName.Render(logEventInfo);
            _interactionService.DisplaySubtleMessage($"Log file written to '{fileName}'");
        }

        return Task.CompletedTask;
    }
}

internal static class LogLocationServiceExtensions
{
    public static ILoggingBuilder AddLogLocationOnExit(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.Services.AddHostedService<LogLocationService>();
        loggingBuilder.Services.TryAddSingleton<IInteractionService, InteractionService>();
        return loggingBuilder;
    }
}
