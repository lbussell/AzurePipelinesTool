// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Humanizer;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PipelineMonitor.AzureDevOps;

internal static class Display
{
    extension(LocalPipelineInfo pipeline)
    {
        public IRenderable SingleLineDisplay =>
            new Markup($"[blue]{pipeline.RelativePath}[/] refers to pipeline [bold green]{pipeline.Name}[/] [dim](ID: {pipeline.Id.Value})[/]");
    }

    extension(PipelineRunInfo run)
    {
        public IRenderable ResultSymbol => new ResultBadge(run.Result);

        public IRenderable RunDetails
        {
            get
            {
                var titleRow = new Markup($"#[bold]{run.Name}[/] • {run.Commit?.Message}");

                var metadataRow = new Columns([
                    new Markup($"[dim]Run by someone...[/]"),
                    new Markup("[dim]•[/]"),
                    new Markup($"[dim]branch/name[/]"),
                    new Markup("[dim]•[/]"),
                    new Markup($"[dim]{run.Commit?.Sha[..10]}[/]"),
                ]);
                metadataRow.Expand = false;

                return new Rows([titleRow, metadataRow]);
            }
        }

        public IRenderable TimeDetails
        {
            get
            {
                var queueTime = run.Started?.Humanize() ?? "";
                var duration = run.Started.HasValue && run.Finished.HasValue
                    ? (run.Finished.Value - run.Started.Value).Humanize(precision: 2, minUnit: TimeUnit.Second)
                    : "";
                return new Rows([
                    new Markup($"[dim]{queueTime}[/]"),
                    new Markup($"[dim]{duration}[/]")
                ]);
            }
        }

        public IRenderable StagesSummary
        {
            get
            {
                var stageCounts = run.Stages
                    .GroupBy(s => s.Result)
                    .ToDictionary(g => g.Key, g => g.Count());

                var badges = new List<IRenderable>();

                // Order: Succeeded, PartiallySucceeded, Failed, Canceled, None
                AddBadgeIfPresent(badges, stageCounts, PipelineRunResult.Succeeded);
                AddBadgeIfPresent(badges, stageCounts, PipelineRunResult.PartiallySucceeded);
                AddBadgeIfPresent(badges, stageCounts, PipelineRunResult.Failed);
                AddBadgeIfPresent(badges, stageCounts, PipelineRunResult.Canceled);
                AddBadgeIfPresent(badges, stageCounts, PipelineRunResult.None);

                if (badges.Count == 0)
                    return new Markup("");

                var columns = new Columns(badges) { Expand = false };
                return columns;
            }
        }

        private static void AddBadgeIfPresent(List<IRenderable> badges, Dictionary<PipelineRunResult, int> counts, PipelineRunResult result)
        {
            if (counts.TryGetValue(result, out var count) && count > 0)
            {
                badges.Add(new ResultBadge(result, count));
            }
        }
    }

    extension(IEnumerable<PipelineRunInfo> runs)
    {
        public IRenderable ToTable()
        {
            var grid = new Grid()
                .AddColumn()
                .AddColumn()
                .AddColumn()
                .AddColumn(new GridColumn().NoWrap().RightAligned());

            grid.AddRow(new Markup(""), new Markup("[dim]Description[/]").PadBottom(), new Markup(""), new Markup(""));

            foreach (var run in runs)
                grid.AddRow(run.ResultSymbol, run.RunDetails.PadBottom(), run.StagesSummary, run.TimeDetails);

            return grid;
        }
    }

    private sealed class ResultBadge(PipelineRunResult result, int? count = null) : IRenderable
    {
        private static readonly (string Symbol, Color Color)[] Styles =
        [
            (" ", Color.Grey),    // None
            ("✓", Color.Green),   // Succeeded
            ("~", Color.Yellow),  // PartiallySucceeded
            ("✗", Color.Red),     // Failed
            ("/", Color.Grey),    // Canceled
        ];

        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            var width = 3 + (count?.ToString().Length ?? 0);
            return new(width, width);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var (symbol, color) = Styles[(int)result];
            var style = new Style(foreground: color);
            yield return new Segment("[", style);
            yield return new Segment(symbol, style);
            yield return new Segment("]", style);
            if (count.HasValue)
                yield return new Segment(count.Value.ToString(), style);
        }
    }
}
