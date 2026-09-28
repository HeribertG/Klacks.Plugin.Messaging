// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Request DTO for creating or updating a messaging provider configuration.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.DTOs;

public record CreateMessagingProviderDto
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ProviderType { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public string ConfigJson { get; init; } = "{}";
}
