// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

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
}
