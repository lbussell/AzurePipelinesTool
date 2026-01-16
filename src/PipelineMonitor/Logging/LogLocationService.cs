// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Targets;

namespace PipelineMonitor.Logging;

internal sealed class LogLocationService : IHostedLifecycleService
{
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
            Console.WriteLine($"Log file: {fileName}");
        }

        return Task.CompletedTask;
    }
}

public static class LogLocationServiceExtensions
{
    public static ILoggingBuilder AddLogLocationOnExit(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.Services.AddHostedService<LogLocationService>();
        return loggingBuilder;
    }
}
