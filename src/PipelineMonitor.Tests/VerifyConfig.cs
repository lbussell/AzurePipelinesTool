// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

namespace PipelineMonitor.Tests;

internal static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("DiffEngine_Disabled", "true");
    }
}
