// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PipelineMonitor.AzureDevOps;

/// <summary>
/// Implementation of <see cref="IProcessRunner"/> using CliWrap.
/// </summary>
internal sealed class CliWrapProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    public async Task<ProcessResult> ExecuteAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        var result = await Cli.Wrap(executable)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(Encoding.UTF8, Encoding.UTF8, cancellationToken);

        return new ProcessResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError);
    }
}

internal static class ProcessRunnerExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection TryAddProcessRunner()
        {
            services.TryAddSingleton<IProcessRunner, CliWrapProcessRunner>();
            return services;
        }
    }
}
