// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// SignalR notification DTO for incoming messages from external providers.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.DTOs;

public record IncomingMessageDto
{
    public Guid MessageId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ProviderDisplayName { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public string SenderDisplayName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
