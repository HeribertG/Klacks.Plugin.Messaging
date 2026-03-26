// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Data transfer object for message display in the API response.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Application.DTOs;

public record MessageDto
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ExternalMessageId { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public string SenderDisplayName { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public string RecipientDisplayName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public MessageDirection Direction { get; init; }
    public MessageStatus Status { get; init; }
    public DateTime Timestamp { get; init; }
    public string? ErrorMessage { get; init; }
    public string? MediaUrl { get; init; }
}
