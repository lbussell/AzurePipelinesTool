// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace PipelineMonitor;

/// <summary>
/// Abstraction for running processes and capturing output.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Executes a command and returns the result.
    /// </summary>
    /// <param name="executable">The executable to run.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="throwOnNonZeroExitCode">Whether to throw an exception on non-zero exit codes. Defaults to true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing exit code and output.</returns>
    Task<ProcessResult> ExecuteAsync(
        string executable,
        string arguments,
        bool throwOnNonZeroExitCode = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a process execution.
/// </summary>
internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
