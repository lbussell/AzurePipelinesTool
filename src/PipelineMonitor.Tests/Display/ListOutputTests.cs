// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class ListOutputTests : VerifyBase
{
    [TestMethod]
    public Task ListOutput()
    {
        using var writer = new StringWriter();
        var markout = new MarkoutWriter(writer);

        markout.WriteTableStart("Name", "ID", "Definition");
        foreach (var pipeline in TestData.SamplePipelines)
            markout.WriteTableRow(pipeline.Name, pipeline.Id.Value.ToString(), pipeline.RelativePath);
        markout.WriteTableEnd();
        markout.Flush();

        return Verify(writer.ToString());
    }
}
