// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using PipelineMonitor.AzureDevOps;
using PipelineMonitor.Commands;

namespace PipelineMonitor.Tests.Display;

[TestClass]
public class WaitOutputTests
{
    [TestMethod]
    public void FormatElapsed_Seconds_FormatsCorrectly()
    {
        var result = WaitCommand.FormatElapsed(TimeSpan.FromSeconds(42));

        Assert.AreEqual("42s", result);
    }

    [TestMethod]
    public void FormatElapsed_Minutes_FormatsCorrectly()
    {
        var result = WaitCommand.FormatElapsed(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)));

        Assert.AreEqual("5m 30s", result);
    }

    [TestMethod]
    public void FormatElapsed_Hours_FormatsCorrectly()
    {
        var result = WaitCommand.FormatElapsed(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(10))));

        Assert.AreEqual("2h 15m 10s", result);
    }

    [TestMethod]
    public void FormatElapsed_Zero_FormatsAsSeconds()
    {
        var result = WaitCommand.FormatElapsed(TimeSpan.Zero);

        Assert.AreEqual("0s", result);
    }

    [TestMethod]
    public void FormatElapsed_JustUnderOneMinute_FormatsAsSeconds()
    {
        var result = WaitCommand.FormatElapsed(TimeSpan.FromSeconds(59));

        Assert.AreEqual("59s", result);
    }

    [TestMethod]
    public void FormatElapsed_ExactlyOneMinute_FormatsAsMinutes()
    {
        var result = WaitCommand.FormatElapsed(TimeSpan.FromMinutes(1));

        Assert.AreEqual("1m 0s", result);
    }

    [TestMethod]
    public void StatusCounts_SucceededTimeline_ShowsAllCompleted()
    {
        var timeline = TestData.SucceededTimeline;
        var states = timeline.Stages.Select(s => s.State);

        var result = FormatStatusCounts("Stages", states);

        StringAssert.Contains(result, "3 completed");
        Assert.DoesNotContain(result, "in progress");
        Assert.DoesNotContain(result, "pending");
    }

    [TestMethod]
    public void StatusCounts_InProgressTimeline_ShowsMixedStates()
    {
        var timeline = TestData.InProgressTimeline;
        var states = timeline.Stages.Select(s => s.State);

        var result = FormatStatusCounts("Stages", states);

        StringAssert.Contains(result, "1 completed");
        StringAssert.Contains(result, "1 in progress");
        StringAssert.Contains(result, "1 pending");
    }

    [TestMethod]
    public void StatusCounts_InProgressTimeline_JobCounts()
    {
        var timeline = TestData.InProgressTimeline;
        var allJobs = timeline.Stages.SelectMany(s => s.Jobs);
        var states = allJobs.Select(j => j.State);

        var result = FormatStatusCounts("Jobs", states);

        StringAssert.Contains(result, "Jobs:");
        StringAssert.Contains(result, "1 completed");
        StringAssert.Contains(result, "1 in progress");
        StringAssert.Contains(result, "1 pending");
    }

    /// <summary>
    /// Mirrors the formatting logic from <see cref="WaitCommand"/>.
    /// </summary>
    private static string FormatStatusCounts(string label, IEnumerable<TimelineRecordStatus> states)
    {
        var counts = states.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());

        List<string> parts = [];
        if (counts.TryGetValue(TimelineRecordStatus.Completed, out var completed))
            parts.Add($"{completed} completed");
        if (counts.TryGetValue(TimelineRecordStatus.InProgress, out var inProgress))
            parts.Add($"{inProgress} in progress");
        if (counts.TryGetValue(TimelineRecordStatus.Pending, out var pending))
            parts.Add($"{pending} pending");

        return $"{label}: {string.Join(", ", parts)}";
    }
}
