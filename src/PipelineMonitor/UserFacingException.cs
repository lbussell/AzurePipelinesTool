// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace PipelineMonitor;

/// <summary>
/// Exception for errors that should be displayed to the user without a stack trace.
/// The message should be user-friendly and actionable.
/// </summary>
internal sealed class UserFacingException : Exception
{
    public UserFacingException(string message) : base(message) { }

    public UserFacingException(string message, Exception innerException) : base(message, innerException) { }
}
