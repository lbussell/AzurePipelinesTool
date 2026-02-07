// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Collections;
using PipelineMonitor.AzureDevOps;
using PipelineMonitor.AzureDevOps.Yaml;
using PipelineMonitor.Commands;
using Spectre.Console;

namespace PipelineMonitor;

internal static class ConsoleDisplayExtensions
{
    private static string SecretPlaceholder => "***";

    extension(IAnsiConsole ansiConsole)
    {
        public void Display(LocalPipelineInfo pipeline)
        {
            ansiConsole.WriteLine($"Pipeline: {pipeline.Name} (ID: {pipeline.Id.Value})");
            ansiConsole.WriteLine($"Definition file: {pipeline.RelativePath}");
        }

        public void DisplayVariables(IReadOnlyList<PipelineVariableInfo> variables)
        {
            foreach (var variable in variables)
                ansiConsole.WriteLine($"- {variable.FormatSingleLine()}");
        }

        public void DisplayParameters(IReadOnlyList<PipelineParameter> parameters)
        {
            foreach (var param in parameters)
                ansiConsole.WriteLine(param.Format());
        }

        private void H1(string text)
        {
            ansiConsole.WriteLine();
            ansiConsole.WriteLine(text);
            ansiConsole.WriteLine(new string('=', text.Length));
        }

        public void H2(string text)
        {
            ansiConsole.WriteLine();
            ansiConsole.WriteLine(text);
            ansiConsole.WriteLine(new string('-', text.Length));
        }

        public void DisplayTimelineProgress(BuildTimelineInfo timeline, TimeSpan elapsed, int nextCheckSeconds)
        {
            var allJobs = timeline.Stages.SelectMany(s => s.Jobs).ToList();

            ansiConsole.WriteLine(FormatStatusCounts("Stages", timeline.Stages.Select(s => s.State)));
            ansiConsole.WriteLine(FormatStatusCounts("Jobs", allJobs.Select(j => j.State)));
            ansiConsole.WriteLine(
                $"Elapsed: {WaitCommand.FormatElapsed(elapsed)}. Next check in {nextCheckSeconds}s..."
            );
            ansiConsole.WriteLine();
        }

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

    extension(PipelineVariableInfo variable)
    {
        private string FormatSingleLine()
        {
            var value = variable.IsSecret ? SecretPlaceholder : (variable.Value ?? "");

            List<string> flagsList = [];
            if (variable.IsSecret) flagsList.Add("secret");
            if (variable.AllowOverride) flagsList.Add("settable");

            var flags = string.Join(", ", flagsList);
            var flagsDisplay = flags.Length > 0 ? $" ({flags})" : "";

            return $"{variable.Name}: {value}{flagsDisplay}";
        }
    }

    extension(PipelineParameter parameter)
    {
        private string Format()
        {
            List<string> lines = [$"- {parameter.Name} ({parameter.ParameterType})"];

            if (parameter.DisplayName is not null)
                lines.Add($"  - Display name: {parameter.DisplayName}");

            if (parameter.Default is not null)
                lines.Add($"  - Default: {FormatValue(parameter.Default)}");

            if (parameter.Values is { Count: > 0 })
            {
                lines.Add("  - Values:");
                lines.AddRange(parameter.Values.Select(v => $"    - {v}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatValue(object value) =>
            value switch
            {
                IDictionary dict => FormatDictionary(dict),
                IList list => FormatList(list),
                _ => value.ToString() ?? "",
            };

        private static string FormatDictionary(IDictionary dict) =>
            "{" + string.Join(", ", dict.Keys.Cast<object>().Select(k => $"{k}: {dict[k]}")) + "}";

        private static string FormatList(IList list) =>
            "[" + string.Join(", ", list.Cast<object>()) + "]";
    }
}
