// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

namespace PipelineMonitor;

// Common models for Azure DevOps concepts.

internal sealed record OrganizationInfo(string Name, Uri Uri);

internal sealed record ProjectInfo(string Name);

internal sealed record RepositoryInfo(string Name, Guid? Id = null);

internal sealed record PipelineInfo(string Name, PipelineId Id, string Url, string Folder);

internal readonly record struct PipelineId(int Value);

internal sealed record LocalPipelineInfo(string Name, FileInfo DefinitionFile, PipelineId Id, string RelativePath);
