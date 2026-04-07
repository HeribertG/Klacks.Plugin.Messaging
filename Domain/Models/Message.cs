// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Represents an inbound or outbound message exchanged via a messaging provider.
/// </summary>
/// <param name="Id">Unique identifier for the message</param>
/// <param name="ProviderId">Foreign key to the MessagingProvider that handled this message</param>
/// <param name="Direction">Whether the message is inbound or outbound</param>
/// <param name="Status">Current delivery status of the message</param>
/// <param name="ClientId">Optional foreign key to the Klacks client resolved via MessengerContact lookup</param>
using System.ComponentModel.DataAnnotations;
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models;

public class Message
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProviderId { get; set; }

    public Guid? ClientId { get; set; }

    public string ExternalMessageId { get; set; } = string.Empty;

    public string Sender { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string RecipientDisplayName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string ContentType { get; set; } = "text";

    public MessageDirection Direction { get; set; }

    public MessageStatus Status { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? ErrorMessage { get; set; }

    public string? MediaUrl { get; set; }

    public virtual MessagingProvider Provider { get; set; } = null!;
}
