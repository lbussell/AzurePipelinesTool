// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xdg.Directories;

namespace AzurePipelinesTool;

/// <summary>
/// Application configuration settings loaded from the XDG config directory.
/// </summary>
internal sealed record AppConfiguration;

/// <summary>
/// Extension methods for adding XDG-compliant configuration to an application.
/// </summary>
internal static class XdgConfigurationExtensions
{
    private const string DefaultFileName = "config.json";

    public static IHostApplicationBuilder AddXdgAppConfiguration(
        this IHostApplicationBuilder builder,
        string appNameDirectory,
        string fileName = DefaultFileName
    )
    {
        var configPath = GetOrCreateConfigFile(appNameDirectory, fileName);
        builder.Configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);

        var configSection = builder.Configuration.GetSection("settings");
        builder.Services.Configure<AppConfiguration>(configSection);

        return builder;
    }

    private static string GetOrCreateConfigFile(string appNameDirectory, string fileName)
    {
        var configDir = Path.Combine(BaseDirectory.ConfigHome, appNameDirectory);
        var configPath = Path.Combine(configDir, fileName);

        if (!File.Exists(configPath))
        {
            Directory.CreateDirectory(configDir);
            File.WriteAllText(configPath, "{}");
        }

        return configPath;
    }
}
