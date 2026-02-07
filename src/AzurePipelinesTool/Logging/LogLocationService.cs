// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool.Display;
using Microsoft.Extensions.Hosting;
using NLog;
using NLog.Targets;

namespace AzurePipelinesTool.Logging;

internal sealed class LogLocationService(InteractionService interactionService) : IHostedLifecycleService
{
    private readonly InteractionService _interactionService = interactionService;

    public Task StartingAsync(CancellationToken _)
    {
        var fileTarget = LogManager.Configuration?.FindTargetByName<FileTarget>("logfile");
        if (fileTarget is null)
            return Task.CompletedTask;
        var logEventInfo = new LogEventInfo { TimeStamp = DateTime.Now };
        var fileName = fileTarget.FileName.Render(logEventInfo);
        _interactionService.DisplaySubtleMessage($"Log file: '{fileName}'");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;
}
