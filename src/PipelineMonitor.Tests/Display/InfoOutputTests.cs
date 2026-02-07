// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;
using PipelineMonitor.Display;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class InfoOutputTests
{
    [TestMethod]
    public void InfoOutput_ContainsPipelineName()
    {
        var output = MarkoutSerializer.Serialize(
            TestData.SamplePipelineInfoView,
            PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "docker-tools-imagebuilder-unofficial");
    }

    [TestMethod]
    public void InfoOutput_ContainsIdAndDefinitionFields()
    {
        var output = MarkoutSerializer.Serialize(
            TestData.SamplePipelineInfoView,
            PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "1513");
        StringAssert.Contains(output, @"eng\pipelines\dotnet-buildtools-image-builder-unofficial.yml");
    }

    [TestMethod]
    public void InfoOutput_ContainsOrganizationContext()
    {
        var output = MarkoutSerializer.Serialize(
            TestData.SamplePipelineInfoView,
            PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "dnceng");
        StringAssert.Contains(output, "internal");
        StringAssert.Contains(output, "dotnet-docker-tools");
    }

    [TestMethod]
    public void InfoOutput_ContainsVariablesSection()
    {
        var output = MarkoutSerializer.Serialize(
            TestData.SamplePipelineInfoView,
            PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "Variables");
        StringAssert.Contains(output, "DisableDockerDetector");
        StringAssert.Contains(output, "imageBuilder.pathArgs");
        StringAssert.Contains(output, "stages");
        StringAssert.Contains(output, "build;test;publish");
    }

    [TestMethod]
    public void InfoOutput_ContainsParametersSection()
    {
        var output = MarkoutSerializer.Serialize(
            TestData.SamplePipelineInfoView,
            PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "Parameters");
        StringAssert.Contains(output, "sourceBuildPipelineRunId");
        StringAssert.Contains(output, "bootstrapImageBuilder");
    }

    [TestMethod]
    public void InfoOutput_WithNoVariablesOrParameters_OmitsSections()
    {
        var view = new PipelineInfoView
        {
            Name = "test-pipeline",
            Id = 42,
            RelativePath = @"eng\test.yml",
            Organization = "myorg",
            Project = "myproject",
            Repository = "myrepo",
            Variables = null,
            Parameters = null,
        };

        var output = MarkoutSerializer.Serialize(view, PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "test-pipeline");
        Assert.DoesNotContain(output, "Variables", "Empty variables section should be omitted");
        Assert.DoesNotContain(output, "Parameters", "Empty parameters section should be omitted");
    }

    [TestMethod]
    public void InfoOutput_SecretVariable_ShowsMaskedValue()
    {
        var view = new PipelineInfoView
        {
            Name = "secret-test",
            Id = 1,
            RelativePath = @"eng\test.yml",
            Organization = "org",
            Project = "proj",
            Repository = "repo",
            Variables =
            [
                VariableRowView.From(new PipelineVariableInfo("apiKey", "super-secret", IsSecret: true, AllowOverride: false)),
            ],
        };

        var output = MarkoutSerializer.Serialize(view, PipelineMonitorMarkoutContext.Default);

        StringAssert.Contains(output, "***");
        Assert.DoesNotContain(output, "super-secret", "Secret value should be masked");
    }
}
