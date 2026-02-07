// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Markout;

namespace AzurePipelinesTool.Display;

[MarkoutContext(typeof(PipelineInfoView))]
[MarkoutContext(typeof(VariableRowView))]
[MarkoutContext(typeof(ParameterRowView))]
public partial class AzurePipelinesToolMarkoutContext : MarkoutSerializerContext;
