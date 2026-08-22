// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Request model for sending a message through a messaging provider.
/// </summary>
/// <param name="Recipient">The recipient address or phone number</param>
/// <param name="Content">The message body to send</param>
/// <param name="ContentType">MIME type of the content (default: "text")</param>
/// <param name="MediaUrl">Optional URL to a media attachment</param>
/// <param name="Actions">Optional fixed set of selectable actions the recipient may answer with. Appended last and defaulted to null so every existing call site stays valid. Only providers reporting IMessagingProviderAdapter.SupportsStructuredActions can carry these; requesting them from any other provider is refused instead of being sent without them.</param>
/// <param name="SenderDisplayName">Optional human-readable name of whoever is sending this message (e.g. "Klacksy" for skill-initiated sends). Null for callers that do not track a sender identity, e.g. the plain REST send endpoint.</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record SendMessageRequest(
    string Recipient,
    string Content,
    string ContentType = "text",
    string? MediaUrl = null,
    IReadOnlyList<MessageAction>? Actions = null,
    string? SenderDisplayName = null);
