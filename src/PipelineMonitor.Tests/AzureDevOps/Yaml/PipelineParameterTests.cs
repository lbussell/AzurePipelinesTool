// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using PipelineMonitor.AzureDevOps.Yaml;

namespace PipelineMonitor.Tests.AzureDevOps.Yaml;

[TestClass]
public class PipelineParameterTests
{
    [TestMethod]
    [DataRow("string", "String")]
    [DataRow("String", "String")]
    [DataRow("STRING", "String")]
    [DataRow("number", "Number")]
    [DataRow("Number", "Number")]
    [DataRow("boolean", "Boolean")]
    [DataRow("Boolean", "Boolean")]
    [DataRow("bool", "Boolean")]
    [DataRow("Bool", "Boolean")]
    [DataRow("object", "Object")]
    [DataRow("Object", "Object")]
    [DataRow("step", "Step")]
    [DataRow("Step", "Step")]
    [DataRow("stepList", "StepList")]
    [DataRow("StepList", "StepList")]
    [DataRow("steplist", "StepList")]
    [DataRow("job", "Job")]
    [DataRow("Job", "Job")]
    [DataRow("jobList", "JobList")]
    [DataRow("JobList", "JobList")]
    [DataRow("joblist", "JobList")]
    [DataRow("deployment", "Deployment")]
    [DataRow("Deployment", "Deployment")]
    [DataRow("deploymentList", "DeploymentList")]
    [DataRow("DeploymentList", "DeploymentList")]
    [DataRow("deploymentlist", "DeploymentList")]
    [DataRow("stage", "Stage")]
    [DataRow("Stage", "Stage")]
    [DataRow("stageList", "StageList")]
    [DataRow("StageList", "StageList")]
    [DataRow("stagelist", "StageList")]
    public void ParameterType_MapsTypeStringCorrectly(string typeString, string expectedTypeName)
    {
        var expected = Enum.Parse<PipelineParameterType>(expectedTypeName);
        var parameter = new PipelineParameter { Name = "test", Type = typeString };

        Assert.AreEqual(expected, parameter.ParameterType);
    }

    [TestMethod]
    [DataRow("unknown")]
    [DataRow("invalid")]
    [DataRow("")]
    [DataRow("int")]
    [DataRow("array")]
    public void ParameterType_UnknownType_DefaultsToString(string typeString)
    {
        var parameter = new PipelineParameter { Name = "test", Type = typeString };

        Assert.AreEqual(PipelineParameterType.String, parameter.ParameterType);
    }

    [TestMethod]
    public void ParameterType_NullType_DefaultsToString()
    {
        var parameter = new PipelineParameter { Name = "test", Type = null! };

        Assert.AreEqual(PipelineParameterType.String, parameter.ParameterType);
    }

    [TestMethod]
    public void DefaultType_IsString()
    {
        var parameter = new PipelineParameter { Name = "test" };

        Assert.AreEqual("string", parameter.Type);
        Assert.AreEqual(PipelineParameterType.String, parameter.ParameterType);
    }

    [TestMethod]
    public void Parameter_WithAllProperties_SetsCorrectly()
    {
        var values = new List<string> { "a", "b", "c" };
        var parameter = new PipelineParameter
        {
            Name = "myParam",
            Type = "string",
            Default = "defaultValue",
            DisplayName = "My Parameter",
            Values = values
        };

        Assert.AreEqual("myParam", parameter.Name);
        Assert.AreEqual("string", parameter.Type);
        Assert.AreEqual("defaultValue", parameter.Default);
        Assert.AreEqual("My Parameter", parameter.DisplayName);
        CollectionAssert.AreEqual(values, parameter.Values);
    }

    [TestMethod]
    public void Parameter_WithMinimalProperties_SetsDefaults()
    {
        var parameter = new PipelineParameter { Name = "minimal" };

        Assert.AreEqual("minimal", parameter.Name);
        Assert.AreEqual("string", parameter.Type);
        Assert.IsNull(parameter.Default);
        Assert.IsNull(parameter.DisplayName);
        Assert.IsNull(parameter.Values);
    }
}
