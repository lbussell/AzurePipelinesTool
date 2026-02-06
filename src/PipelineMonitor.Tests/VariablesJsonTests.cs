// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace PipelineMonitor.Tests;

[TestClass]
public sealed class VariablesJsonTests
{
    [TestMethod]
    public void VariableInfo_SerializesToJson()
    {
        var variables = new List<PipelineVariableInfo>
        {
            new("TestVar1", "Value1", false, true),
            new("TestVar2", "Value2", true, false),
            new("TestVar3", "", false, false),
        };

        var json = JsonSerializer.Serialize(variables, new JsonSerializerOptions { WriteIndented = true });

        Assert.IsNotNull(json);
        StringAssert.Contains(json, "TestVar1");
        StringAssert.Contains(json, "Value1");
        StringAssert.Contains(json, "TestVar2");
        StringAssert.Contains(json, "Value2");
    }

    [TestMethod]
    public void VariableInfo_DeserializesFromJson()
    {
        var json = """
            [
              {
                "Name": "TestVar1",
                "Value": "Value1",
                "IsSecret": false,
                "AllowOverride": true
              },
              {
                "Name": "TestVar2",
                "Value": "Value2",
                "IsSecret": true,
                "AllowOverride": false
              }
            ]
            """;

        var variables = JsonSerializer.Deserialize<List<PipelineVariableInfo>>(json);

        Assert.IsNotNull(variables);
        Assert.HasCount(2, variables);
        Assert.AreEqual("TestVar1", variables[0].Name);
        Assert.AreEqual("Value1", variables[0].Value);
        Assert.IsFalse(variables[0].IsSecret);
        Assert.IsTrue(variables[0].AllowOverride);
        Assert.AreEqual("TestVar2", variables[1].Name);
        Assert.AreEqual("Value2", variables[1].Value);
        Assert.IsTrue(variables[1].IsSecret);
        Assert.IsFalse(variables[1].AllowOverride);
    }

    [TestMethod]
    public void VariableInfo_RoundTripSerialization()
    {
        var originalVariables = new List<PipelineVariableInfo>
        {
            new("TestVar1", "Value1", false, true),
            new("TestVar2", "Value2", true, false),
            new("TestVar3", "Value3", false, false),
        };

        var json = JsonSerializer.Serialize(originalVariables);
        var deserializedVariables = JsonSerializer.Deserialize<List<PipelineVariableInfo>>(json);

        Assert.IsNotNull(deserializedVariables);
        Assert.HasCount(originalVariables.Count, deserializedVariables);
        for (int i = 0; i < originalVariables.Count; i++)
        {
            Assert.AreEqual(originalVariables[i].Name, deserializedVariables[i].Name);
            Assert.AreEqual(originalVariables[i].Value, deserializedVariables[i].Value);
            Assert.AreEqual(originalVariables[i].IsSecret, deserializedVariables[i].IsSecret);
            Assert.AreEqual(originalVariables[i].AllowOverride, deserializedVariables[i].AllowOverride);
        }
    }

    [TestMethod]
    public void VariableInfo_InvalidJson_ThrowsJsonException()
    {
        var invalidJson = "{ invalid json }";
        var exceptionThrown = false;

        try
        {
            JsonSerializer.Deserialize<List<PipelineVariableInfo>>(invalidJson);
        }
        catch (JsonException)
        {
            exceptionThrown = true;
        }

        Assert.IsTrue(exceptionThrown, "Expected JsonException to be thrown for invalid JSON");
    }
}
