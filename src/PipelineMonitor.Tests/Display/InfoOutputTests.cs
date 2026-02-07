// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;
using PipelineMonitor.Display;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class InfoOutputTests : VerifyBase
{
    [TestMethod]
    public Task InfoOutput_FullPipeline()
    {
        var output = MarkoutSerializer.Serialize(
            TestData.SamplePipelineInfoView,
            PipelineMonitorMarkoutContext.Default);

        return Verify(output);
    }

    [TestMethod]
    public Task InfoOutput_NoVariablesOrParameters()
    {
        var view = new PipelineInfoView
        {
            Name = "test-pipeline",
            Id = 42,
            RelativePath = "eng/test.yml",
            Organization = "myorg",
            Project = "myproject",
            Repository = "myrepo",
            Variables = null,
            Parameters = null,
        };

        var output = MarkoutSerializer.Serialize(view, PipelineMonitorMarkoutContext.Default);

        return Verify(output);
    }

    [TestMethod]
    public Task InfoOutput_SecretVariable()
    {
        var view = new PipelineInfoView
        {
            Name = "secret-test",
            Id = 1,
            RelativePath = "eng/test.yml",
            Organization = "org",
            Project = "proj",
            Repository = "repo",
            Variables =
            [
                VariableRowView.From(new PipelineVariableInfo("apiKey", "super-secret", IsSecret: true, AllowOverride: false)),
            ],
        };

        var output = MarkoutSerializer.Serialize(view, PipelineMonitorMarkoutContext.Default);

        return Verify(output);
    }
}
