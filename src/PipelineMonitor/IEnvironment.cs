// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace PipelineMonitor;

/// <summary>
/// Abstraction over environment details such as the current process path and working directory.
/// </summary>
internal interface IEnvironment
{
    string? ProcessPath { get; }
    string CurrentDirectory { get; }
}

internal sealed class SystemEnvironment : IEnvironment
{
    public string? ProcessPath => Environment.ProcessPath;
    public string CurrentDirectory => Environment.CurrentDirectory;
}
