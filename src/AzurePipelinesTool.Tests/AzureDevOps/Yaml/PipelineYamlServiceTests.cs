// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using AzurePipelinesTool.AzureDevOps.Yaml;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzurePipelinesTool.Tests.AzureDevOps.Yaml;

[TestClass]
public class PipelineYamlServiceTests
{
    private PipelineYamlService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new PipelineYamlService(NullLogger<PipelineYamlService>.Instance);
    }

    [TestMethod]
    public void Parse_WithValidParameters_ReturnsParameters()
    {
        var yaml = """
            parameters:
            - name: environment
              type: string
              default: 'dev'
            - name: runTests
              type: boolean
              default: true
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.HasCount(2, result.Parameters);
        Assert.AreEqual("environment", result.Parameters[0].Name);
        Assert.AreEqual("string", result.Parameters[0].Type);
        Assert.AreEqual("dev", result.Parameters[0].Default);
        Assert.AreEqual("runTests", result.Parameters[1].Name);
        Assert.AreEqual("boolean", result.Parameters[1].Type);
        Assert.AreEqual("true", result.Parameters[1].Default?.ToString());
    }

    [TestMethod]
    public void Parse_WithNoParametersSection_ReturnsEmptyList()
    {
        var yaml = """
            trigger:
              - main
            pool:
              vmImage: ubuntu-latest
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.HasCount(0, result.Parameters);
    }

    [TestMethod]
    public void Parse_WithEmptyParametersSection_ReturnsEmptyList()
    {
        var yaml = """
            parameters: []
            trigger:
              - main
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.HasCount(0, result.Parameters);
    }

    [TestMethod]
    public void Parse_WithEmptyYaml_ReturnsNull()
    {
        var yaml = "";

        var result = _service.Parse(yaml);

        // YamlDotNet returns null when deserializing an empty string
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Parse_WithMalformedYaml_ReturnsNull()
    {
        var yaml = """
            parameters:
              - name: test
                invalid yaml here {{{{
            """;

        var result = _service.Parse(yaml);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Parse_WithAllowedValues_ReturnsValuesList()
    {
        var yaml = """
            parameters:
            - name: environment
              type: string
              default: 'dev'
              values:
              - dev
              - staging
              - prod
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.HasCount(1, result.Parameters);
        Assert.IsNotNull(result.Parameters[0].Values);
        CollectionAssert.AreEqual(new[] { "dev", "staging", "prod" }, result.Parameters[0].Values);
    }

    [TestMethod]
    public void Parse_WithDisplayName_ReturnsDisplayName()
    {
        var yaml = """
            parameters:
            - name: env
              displayName: 'Target Environment'
              type: string
              default: 'dev'
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.AreEqual("Target Environment", result.Parameters[0].DisplayName);
    }

    [TestMethod]
    public void Parse_WithNoDefault_ReturnsNullDefault()
    {
        var yaml = """
            parameters:
            - name: environment
              type: string
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.IsNull(result.Parameters[0].Default);
    }

    [TestMethod]
    public void Parse_WithNumberType_ParsesCorrectly()
    {
        var yaml = """
            parameters:
            - name: retryCount
              type: number
              default: 3
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.AreEqual("number", result.Parameters[0].Type);
        Assert.AreEqual("3", result.Parameters[0].Default?.ToString());
    }

    [TestMethod]
    public void Parse_WithObjectDefault_ParsesAsObject()
    {
        var yaml = """
            parameters:
            - name: config
              type: object
              default:
                key1: value1
                key2: value2
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.AreEqual("object", result.Parameters[0].Type);
        Assert.IsNotNull(result.Parameters[0].Default);
    }

    [TestMethod]
    public void Parse_WithNoTypeSpecified_DefaultsToString()
    {
        var yaml = """
            parameters:
            - name: simpleParam
              default: 'value'
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.AreEqual("string", result.Parameters[0].Type);
    }

    [TestMethod]
    public async Task ParseAsync_WithNonexistentFile_ReturnsNull()
    {
        var result = await _service.ParseAsync("/nonexistent/path/to/file.yml");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ParseAsync_WithValidFile_ParsesCorrectly()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                parameters:
                - name: test
                  type: string
                  default: 'hello'
                """
            );

            var result = await _service.ParseAsync(tempFile);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Parameters);
            Assert.HasCount(1, result.Parameters);
            Assert.AreEqual("test", result.Parameters[0].Name);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void Parse_WithStepType_ParsesCorrectly()
    {
        var yaml = """
            parameters:
            - name: customStep
              type: step
              default:
                script: echo hello
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.AreEqual("step", result.Parameters[0].Type);
        Assert.IsNotNull(result.Parameters[0].Default);
    }

    [TestMethod]
    public void Parse_WithStepListType_ParsesCorrectly()
    {
        var yaml = """
            parameters:
            - name: steps
              type: stepList
              default: []
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.AreEqual("stepList", result.Parameters[0].Type);
    }

    [TestMethod]
    public void Parse_IgnoresUnknownFields_ParsesSuccessfully()
    {
        var yaml = """
            name: MyPipeline
            trigger:
              - main
            variables:
              foo: bar
            parameters:
            - name: test
              type: string
            stages:
            - stage: Build
              jobs:
              - job: BuildJob
            """;

        var result = _service.Parse(yaml);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Parameters);
        Assert.HasCount(1, result.Parameters);
        Assert.AreEqual("test", result.Parameters[0].Name);
    }
}
