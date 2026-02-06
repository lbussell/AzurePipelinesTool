// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PipelineMonitor.AzureDevOps.Yaml;

internal sealed class PipelineYamlService(ILogger<PipelineYamlService> logger)
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
