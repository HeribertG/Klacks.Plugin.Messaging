// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Data transfer object for sending a broadcast message to specific clients by id number.
/// </summary>
/// <param name="Provider">Name of the messaging provider to use</param>
/// <param name="IdNumbers">Array of client id numbers to broadcast to</param>
/// <param name="Content">Message content to send</param>
/// <param name="ContentType">Content type, defaults to "text"</param>

namespace Klacks.Plugin.Messaging.Application.DTOs;

public record SendBroadcastToIdNumbersDto
{
    public string Provider { get; init; } = string.Empty;
    public int[] IdNumbers { get; init; } = Array.Empty<int>();
    public string Content { get; init; } = string.Empty;
    public string? ContentType { get; init; } = "text";
}
