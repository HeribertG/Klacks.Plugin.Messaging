// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Request DTO for sending a broadcast message to all clients of a Klacks group.
/// The server resolves recipients via MessengerContact and (when supported by the provider)
/// falls back to the client's mobile phone number.
/// </summary>

namespace Klacks.Plugin.Messaging.Application.DTOs;

public record SendBroadcastDto
{
    public string Provider { get; init; } = string.Empty;
    public Guid GroupId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ContentType { get; init; } = "text";
}
