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
        public IRenderable ResultSymbol => new PipelineRunResultRenderable(run.Result);

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
    }

    extension(IEnumerable<PipelineRunInfo> runs)
    {
        public IRenderable ToTable()
        {
            var grid = new Grid()
                .AddColumn()
                .AddColumn()
                .AddColumn(new GridColumn().NoWrap().RightAligned());

            grid.AddRow(new Markup(""), new Markup("[dim]Description[/]").PadBottom(), new Markup(""));

            foreach (var run in runs)
                grid.AddRow(run.ResultSymbol, run.RunDetails.PadBottom(), run.TimeDetails);

            return grid;
        }
    }

    private sealed class PipelineRunResultRenderable(PipelineRunResult result) : IRenderable
    {
        private static readonly (string Symbol, Color Color)[] Styles =
        [
            ("?", Color.Grey),    // None
            ("✓", Color.Green),   // Succeeded
            ("~", Color.Yellow),  // PartiallySucceeded
            ("✗", Color.Red),     // Failed
            ("!", Color.Grey),    // Canceled
        ];

        public Measurement Measure(RenderOptions options, int maxWidth) => new(3, 3);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var (symbol, color) = Styles[(int)result];
            var style = new Style(foreground: color);
            yield return new Segment("[", style);
            yield return new Segment(symbol, style);
            yield return new Segment("]", style);
        }
    }
}
