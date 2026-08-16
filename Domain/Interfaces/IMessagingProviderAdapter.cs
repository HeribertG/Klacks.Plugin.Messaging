// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Adapter interface for external messaging provider APIs (Telegram, WhatsApp, Slack, Microsoft Teams, SMS and others).
/// Each provider implements this to handle sending messages and parsing webhooks.
/// </summary>
/// <param name="ProviderType">The constant identifying which provider this adapter handles</param>
/// <param name="configJson">Provider-specific configuration JSON containing API keys and settings</param>
/// <param name="request">The outbound message to send</param>
/// <param name="body">Raw webhook request body</param>
/// <param name="context">Validation context with body, headers, config and webhook secret</param>
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessagingProviderAdapter
{
    string ProviderType { get; }

    /// <summary>
    /// Indicates whether this provider accepts a plain mobile phone number as recipient.
    /// True for WhatsApp, Signal and SMS. False for Telegram, Threema, Viber, LINE, KakaoTalk,
    /// WeChat, Zalo, Microsoft Teams, Slack where an explicit external identifier is required.
    /// Used by the broadcast flow to fall back to Client.Communication when no MessengerContact
    /// exists for the target client.
    /// </summary>
    bool SupportsPhoneAsRecipient { get; }

    /// <summary>
    /// Indicates whether this provider can carry a fixed set of selectable actions to the recipient
    /// and receive the chosen one back.
    /// True for Telegram, Slack, LINE, Viber and WhatsApp - exactly the providers whose
    /// ParseWebhookPayload can return an inbound message at all.
    /// False for KakaoTalk, Signal, SMS, Microsoft Teams, Threema, WeChat and Zalo, whose
    /// ParseWebhookPayload always returns null: an answer could never arrive there, so offering a
    /// choice would strand the recipient with a question they cannot reply to. Over those seven a
    /// message can only notify, never ask.
    /// What the flag promises is the round trip, not the rendering. No adapter renders native buttons
    /// yet - Telegram inline keyboards, Slack Block Kit and their equivalents are still unimplemented.
    /// Until one does, a numbered reply carries the same fixed set over the same five providers and
    /// serves the same purpose: no free-text interpretation, no model on the answering side, and an
    /// exact audit record.
    /// Used by SendMessageRequest.Actions: requesting actions from a provider that reports false is
    /// refused and recorded as a failed message, rather than silently sent without the choices.
    /// </summary>
    bool SupportsStructuredActions { get; }

    Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default);

    Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default);

    WebhookValidationResult ValidateWebhook(WebhookValidationContext context);

    IncomingMessage? ParseWebhookPayload(string body);
}
