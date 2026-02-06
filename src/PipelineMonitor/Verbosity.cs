// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

using Microsoft.Extensions.DependencyInjection;

namespace PipelineMonitor;

internal sealed record GlobalOptions(bool Verbose = false)
{
    public static GlobalOptions Default => new();
};

internal static class GlobalOptionsExtensions
{
    public static void AddGlobalOptions(this ConsoleApp.ConsoleAppBuilder builder)
    {
        builder.ConfigureGlobalOptions((ref ConsoleApp.GlobalOptionsBuilder b) =>
        {
            var verbosity = b.AddGlobalOption<bool>("-v|--verbose", "Show more detailed information.");
            return new GlobalOptions(verbosity);
        });

        builder.ConfigureServices((context, _, services) =>
        {
            var globalOptions = (context.GlobalOptions as GlobalOptions) ?? GlobalOptions.Default;
            services.AddSingleton(globalOptions);
        });
    }
}
