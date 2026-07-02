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

    Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default);

    Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default);

    WebhookValidationResult ValidateWebhook(WebhookValidationContext context);

    IncomingMessage? ParseWebhookPayload(string body);
}
