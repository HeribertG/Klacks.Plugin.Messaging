// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants for the messaging subsystem including provider names, defaults and settings keys.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class MessagingConstants
{
    public const string PluginName = "messaging";

    public const string ProviderWhatsApp = "WhatsApp";
    public const string ProviderTelegram = "Telegram";
    public const string ProviderSignal = "Signal";
    public const string ProviderSms = "SMS";

    public const int DefaultRetentionCount = 1000;
    public const string DefaultContentType = "text";

    public const string SettingRetentionCount = "MESSAGE_RETENTION_COUNT";
    public const string SettingWebhookBaseUrl = "MESSAGING_WEBHOOK_BASE_URL";
}
