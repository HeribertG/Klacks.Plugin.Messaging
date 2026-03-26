// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// DTO representing a message received from a provider webhook.
/// </summary>
/// <param name="ExternalMessageId">Provider-assigned identifier for the message</param>
/// <param name="Sender">Sender address or phone number</param>
/// <param name="SenderDisplayName">Human-readable name of the sender</param>
/// <param name="Content">The message body</param>
/// <param name="ContentType">MIME type of the content (default: "text")</param>
/// <param name="MediaUrl">Optional URL to a media attachment</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record IncomingMessage(
    string ExternalMessageId,
    string Sender,
    string SenderDisplayName,
    string Content,
    string ContentType = "text",
    string? MediaUrl = null);
