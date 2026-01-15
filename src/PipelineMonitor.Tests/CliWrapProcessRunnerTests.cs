// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace PipelineMonitor.Tests;

[TestClass]
public sealed class CliWrapProcessRunnerTests
{
    [TestMethod]
    public async Task RunAsync_ExecutesCommandSuccessfully()
    {
        // Arrange
        var runner = new CliWrapProcessRunner();

        // Act - use dotnet command which is available on all platforms where tests run
        var result = await runner.RunAsync("dotnet", "--version");

        // Assert
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_CapturesNonZeroExitCode()
    {
        // Arrange
        var runner = new CliWrapProcessRunner();

        // Act - use dotnet command with invalid arguments to get non-zero exit code
        var result = await runner.RunAsync("dotnet", "invalid-command-that-does-not-exist");

        // Assert
        Assert.AreNotEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_CapturesStandardError()
    {
        // Arrange
        var runner = new CliWrapProcessRunner();

        // Act - use dotnet command with invalid arguments to get error output
        var result = await runner.RunAsync("dotnet", "invalid-command-that-does-not-exist");

        // Assert - dotnet writes errors to stderr or stdout depending on the error
        // At least one of them should contain error information
        var hasStdErr = !string.IsNullOrWhiteSpace(result.StandardError);
        var hasStdOut = !string.IsNullOrWhiteSpace(result.StandardOutput);
        Assert.IsTrue(hasStdErr || hasStdOut, "Expected error output in either StandardError or StandardOutput");
    }

    [TestMethod]
    public async Task RunAsync_UsesWorkingDirectory()
    {
        // Arrange
        var runner = new CliWrapProcessRunner();
        var workingDir = Path.GetTempPath();
        var (command, args) = GetPrintWorkingDirectoryCommand();

        // Act - run command to print working directory
        var result = await runner.RunAsync(command, args, workingDir);

        // Assert
        Assert.AreEqual(0, result.ExitCode);
        var outputPath = result.StandardOutput.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedPath = workingDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.AreEqual(expectedPath, outputPath, ignoreCase: true);
    }

    private static (string Command, string Args) GetPrintWorkingDirectoryCommand()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("cmd.exe", "/c cd");
        }
        else
        {
            return ("pwd", "");
        }
    }
}
