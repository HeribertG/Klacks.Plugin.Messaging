// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Request model for sending a message through a messaging provider.
/// </summary>
/// <param name="Recipient">The recipient address or phone number</param>
/// <param name="Content">The message body to send</param>
/// <param name="ContentType">MIME type of the content (default: "text")</param>
/// <param name="MediaUrl">Optional URL to a media attachment</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record SendMessageRequest(
    string Recipient,
    string Content,
    string ContentType = "text",
    string? MediaUrl = null);
