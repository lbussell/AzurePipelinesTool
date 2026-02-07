// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class ListOutputTests
{
    [TestMethod]
    public void ListOutput_ContainsTableHeaders()
    {
        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);

        markout.WriteTableStart("Name", "ID", "Definition");
        foreach (var pipeline in TestData.SamplePipelines)
            markout.WriteTableRow(pipeline.Name, pipeline.Id.Value.ToString(), pipeline.RelativePath);
        markout.WriteTableEnd();
        markout.Flush();

        var output = writer.ToString();
        StringAssert.Contains(output, "| Name |");
        StringAssert.Contains(output, "| ID |");
        StringAssert.Contains(output, "| Definition |");
    }

    [TestMethod]
    public void ListOutput_ContainsAllPipelines()
    {
        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);

        markout.WriteTableStart("Name", "ID", "Definition");
        foreach (var pipeline in TestData.SamplePipelines)
            markout.WriteTableRow(pipeline.Name, pipeline.Id.Value.ToString(), pipeline.RelativePath);
        markout.WriteTableEnd();
        markout.Flush();

        var output = writer.ToString();
        foreach (var pipeline in TestData.SamplePipelines)
        {
            StringAssert.Contains(output, pipeline.Name);
            StringAssert.Contains(output, pipeline.Id.Value.ToString());
            StringAssert.Contains(output, pipeline.RelativePath);
        }
    }

    [TestMethod]
    public void ListOutput_HasCorrectRowCount()
    {
        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);

        markout.WriteTableStart("Name", "ID", "Definition");
        foreach (var pipeline in TestData.SamplePipelines)
            markout.WriteTableRow(pipeline.Name, pipeline.Id.Value.ToString(), pipeline.RelativePath);
        markout.WriteTableEnd();
        markout.Flush();

        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Header row + separator row + data rows
        var dataRows = lines.Count(l => l.TrimStart().StartsWith('|')) - 2;
        Assert.AreEqual(TestData.SamplePipelines.Count, dataRows);
    }
}
