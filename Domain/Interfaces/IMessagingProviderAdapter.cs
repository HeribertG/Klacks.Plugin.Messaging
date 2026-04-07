// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Adapter interface for external messaging provider APIs (WhatsApp, Telegram, Signal, SMS).
/// Each provider implements this to handle sending messages and parsing webhooks.
/// </summary>
/// <param name="ProviderType">The constant identifying which provider this adapter handles</param>
/// <param name="configJson">Provider-specific configuration JSON containing API keys and settings</param>
/// <param name="request">The outbound message to send</param>
/// <param name="body">Raw webhook request body</param>
/// <param name="signature">Webhook signature header for validation</param>
/// <param name="webhookSecret">Expected secret for webhook validation</param>
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessagingProviderAdapter
{
    string ProviderType { get; }

    /// <summary>
    /// Indicates whether this provider accepts a plain mobile phone number as recipient.
    /// True for WhatsApp, Signal, SMS, Viber. False for Telegram, Threema, LINE, KakaoTalk,
    /// WeChat, Zalo, Microsoft Teams, Slack where an explicit external identifier is required.
    /// Used by the broadcast flow to fall back to Client.Communication when no MessengerContact
    /// exists for the target client.
    /// </summary>
    bool SupportsPhoneAsRecipient { get; }

    Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default);

    Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default);

    WebhookValidationResult ValidateWebhook(string body, string signature, string webhookSecret);

    IncomingMessage? ParseWebhookPayload(string body);
}
