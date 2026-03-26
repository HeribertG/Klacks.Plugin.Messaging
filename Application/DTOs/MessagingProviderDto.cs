// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Data transfer object for messaging provider configuration display.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.DTOs;

public record MessagingProviderDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ProviderType { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
