// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Microsoft.Extensions.Logging;

namespace PipelineMonitor.Filters;

internal sealed class ExceptionHandlingFilter(
    InteractionService interactionService,
    ILogger<ExceptionHandlingFilter> logger,
    ConsoleAppFilter next
) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Operation was cancelled");
        }
        catch (UserFacingException ex)
        {
            logger.LogError(ex, "User-facing error occurred");
            interactionService.DisplayError(ex.Message);
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred");
            interactionService.DisplayError("An unexpected error occurred. Check logs for details.");
            Environment.ExitCode = 1;
        }
    }
}
