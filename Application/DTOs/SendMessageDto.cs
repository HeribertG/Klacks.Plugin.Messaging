// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Request DTO for sending a message via a messaging provider.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.DTOs;

public record SendMessageDto
{
    public string Provider { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ContentType { get; init; } = "text";
    public string? MediaUrl { get; init; }
}
