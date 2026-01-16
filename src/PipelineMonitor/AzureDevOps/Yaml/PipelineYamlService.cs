// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PipelineMonitor.AzureDevOps.Yaml;

/// <summary>
/// Service for parsing Azure Pipeline YAML files.
/// </summary>
internal interface IPipelineYamlService
{
    /// <summary>
    /// Parses a pipeline YAML file and extracts parameter definitions.
    /// </summary>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed pipeline YAML with parameters, or null if parsing fails.</returns>
    Task<PipelineYaml?> ParseAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses pipeline YAML content from a string.
    /// </summary>
    /// <param name="yamlContent">YAML content as a string.</param>
    /// <returns>Parsed pipeline YAML with parameters, or null if parsing fails.</returns>
    PipelineYaml? Parse(string yamlContent);
}

internal sealed class PipelineYamlService(ILogger<PipelineYamlService> logger) : IPipelineYamlService
{
    private readonly ILogger<PipelineYamlService> _logger = logger;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<PipelineYaml?> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Pipeline YAML file not found: {FilePath}", filePath);
                return null;
            }

            var yamlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return Parse(yamlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse pipeline YAML file: {FilePath}", filePath);
            return null;
        }
    }

    public PipelineYaml? Parse(string yamlContent)
    {
        try
        {
            var pipeline = Deserializer.Deserialize<PipelineYaml>(yamlContent);
            return pipeline;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            _logger.LogError(ex, "YAML parsing error: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing YAML");
            return null;
        }
    }
}

internal static class PipelineYamlServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddPipelineYamlService()
        {
            services.TryAddSingleton<IPipelineYamlService, PipelineYamlService>();
            return services;
        }
    }
}
