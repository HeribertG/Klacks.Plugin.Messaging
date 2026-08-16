// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants for the messaging subsystem including provider names, defaults and settings keys.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class MessagingConstants
{
    public const string PluginName = "messaging";

    /// <summary>
    /// Role name the host issues as a role claim. Kept as a constant because it is used both in an
    /// attribute argument and in an imperative check, and the two must never drift apart.
    /// </summary>
    public const string RoleAdmin = "Admin";

    public const string ProviderWhatsApp = "WhatsApp";
    public const string ProviderTelegram = "Telegram";
    public const string ProviderSignal = "Signal";
    public const string ProviderSms = "SMS";
    public const string ProviderThreema = "Threema";
    public const string ProviderViber = "Viber";
    public const string ProviderLine = "LINE";
    public const string ProviderKakaoTalk = "KakaoTalk";
    public const string ProviderWeChat = "WeChat";
    public const string ProviderZalo = "Zalo";
    public const string ProviderTeams = "MicrosoftTeams";
    public const string ProviderSlack = "Slack";

    public const int DefaultRetentionCount = 1000;
    public const string DefaultContentType = "text";

    public const string SettingRetentionCount = "MESSAGE_RETENTION_COUNT";
    public const string SettingWebhookBaseUrl = "MESSAGING_WEBHOOK_BASE_URL";

    /// <summary>
    /// JSONB settings key holding the owner's messenger identities.
    /// Shape: [{ "type": int (MessengerType), "value": string, "description": string? }]
    /// </summary>
    public const string SettingOwnerMessengers = "APP_OWNER_MESSENGERS";

    public const string SettingTelegramBotUsername = "TELEGRAM_BOT_USERNAME";
    public const int BotUsernameCacheMinutes = 1440;

    /// <summary>
    /// Event type broadcast on the plugin event bus whenever an inbound message was stored.
    /// </summary>
    public const string IncomingMessageEventType = "messaging.incoming";

    /// <summary>
    /// Seconds between polling rounds for providers that cannot deliver through a webhook.
    /// </summary>
    public const int InboundPollIntervalSeconds = 10;

    /// <summary>
    /// Settings key prefix holding the per-provider polling cursor; the provider name is appended.
    /// </summary>
    public const string InboundPollCursorSettingPrefix = "MESSAGING_POLL_CURSOR_";

    /// <summary>
    /// Minutes for which a discarded inbound sender is logged only once, per provider and sender.
    /// Without a window, a bot writing continuously would produce one log line per message.
    /// </summary>
    public const int UnknownSenderLogSuppressionMinutes = 60;

    /// <summary>
    /// Error recorded when structured actions are requested from a provider that cannot carry them.
    /// Placeholder 0 is the provider name.
    /// </summary>
    public const string StructuredActionsUnsupportedErrorFormat =
        "Provider '{0}' cannot carry structured actions: the recipient would have no way to answer";
}
