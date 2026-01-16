// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace PipelineMonitor.Logging;

internal static class FileLogging
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder loggingBuilder, IConfiguration configuration)
    {
        loggingBuilder.AddNLog(configuration);
        return loggingBuilder;
    }
}
