// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

namespace PipelineMonitor.Commands;

internal sealed class InfoCommand(
    PipelineResolver pipelineResolver)
{
    private readonly PipelineResolver _pipelineResolver = pipelineResolver;

    [Command("info")]
    public async Task ExecuteAsync([Argument] string definitionPath)
    {
        var pipeline = await _pipelineResolver.GetLocalPipelineAsync(definitionPath);
        Console.WriteLine($"{pipeline.RelativePath} -> {pipeline.Name} (ID: {pipeline.Id.Value})");
    }
}
