// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Request model for sending a message through a messaging provider.
/// </summary>
/// <param name="Recipient">The recipient address or phone number</param>
/// <param name="Content">The message body to send</param>
/// <param name="ContentType">MIME type of the content (default: "text")</param>
/// <param name="MediaUrl">Optional URL to a media attachment</param>
/// <param name="Actions">Optional fixed set of selectable actions the recipient may answer with. Appended last and defaulted to null so every existing call site stays valid. Only providers reporting IMessagingProviderAdapter.SupportsStructuredActions can carry these; requesting them from any other provider is refused instead of being sent without them.</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record SendMessageRequest(
    string Recipient,
    string Content,
    string ContentType = "text",
    string? MediaUrl = null,
    IReadOnlyList<MessageAction>? Actions = null);
