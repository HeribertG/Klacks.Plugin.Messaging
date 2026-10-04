// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// SignalR notification DTO for incoming messages from external providers. It is broadcast to every connected
/// user regardless of role or group visibility, so it only says that a message arrived: sender, chat id and
/// content stay out and are read through the visibility-filtered message routes.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.DTOs;

public record IncomingMessageDto
{
    public Guid MessageId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ProviderDisplayName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
