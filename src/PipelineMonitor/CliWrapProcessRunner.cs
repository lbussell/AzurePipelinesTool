// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text;
using CliWrap;
using CliWrap.Buffered;

namespace PipelineMonitor;

/// <summary>
/// Implementation of <see cref="IProcessRunner"/> using CliWrap.
/// </summary>
internal sealed class CliWrapProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    public async Task<ProcessResult> ExecuteAsync(
        string executable,
        string arguments,
        bool allowNonZeroExitCode = false,
        CancellationToken cancellationToken = default
    )
    {
        var validation = allowNonZeroExitCode ? CommandResultValidation.None : CommandResultValidation.ZeroExitCode;

        var result = await Cli.Wrap(executable)
            .WithArguments(arguments)
            .WithValidation(validation)
            .ExecuteBufferedAsync(Encoding.UTF8, Encoding.UTF8, cancellationToken);

        return new ProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
