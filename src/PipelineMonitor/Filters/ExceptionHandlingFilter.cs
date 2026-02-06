// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PipelineMonitor.Filters;

internal sealed class ExceptionHandlingFilter(
    IAnsiConsole ansiConsole,
    InteractionService interactionService,
    ILogger<ExceptionHandlingFilter> logger,
    ConsoleAppFilter next
) : ConsoleAppFilter(next)
{
    private const string GitHubRepoUrl = "https://github.com/lbussell/PipelineMonitor";

    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger?.LogDebug("Operation was cancelled");
        }
        catch (UserFacingException ex)
        {
            logger?.LogError(ex, "User-facing error occurred");
            interactionService.DisplayError(ex.Message);
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error occurred");
            Environment.ExitCode = 1;
            DisplayUnexpectedError(context, ex);
        }
    }

    private void DisplayUnexpectedError(ConsoleAppContext context, Exception ex)
    {
        try
        {
            interactionService.DisplayError("An unexpected error occurred.");
            ansiConsole.WriteException(ex);
            ansiConsole.WriteLine();

            var issueUrl = BuildGitHubIssueUrl(context, ex);
            ansiConsole.MarkupLineInterpolated($"[link={issueUrl}]Please click here to report this issue on GitHub.[/]");
            ansiConsole.WriteLine("The link will automatically fill in the exception details for you.");
        }
        catch
        {
            // Last resort if Spectre or InteractionService is broken
            Console.Error.WriteLine($"An unexpected error occurred: {ex}");
            Console.Error.WriteLine($"Please report this issue: {GitHubRepoUrl}/issues");
        }
    }

    private static string BuildGitHubIssueUrl(ConsoleAppContext context, Exception ex)
    {
        var args = string.Join(" ", context.Arguments);
        var title = Uri.EscapeDataString($"Unexpected {ex.GetType().Name}: {ex.Message}");
        var body = Uri.EscapeDataString(
            $"""
            ### Command line arguments

            ```
            {args}
            ```

            ### Exception Details

            ```
            {ex}
            ```
            """);

        return $"{GitHubRepoUrl}/issues/new?title={title}&body={body}&labels=bug";
    }
}
