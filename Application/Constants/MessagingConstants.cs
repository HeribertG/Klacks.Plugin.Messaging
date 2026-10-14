// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

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

    /// <summary>
    /// Supervisor role claim the host issues; together with RoleAdmin it may edit client data.
    /// </summary>
    public const string RoleSupervisor = "Authorised";

    public const string RolesClientEditors = RoleAdmin + "," + RoleSupervisor;

    /// <summary>
    /// Error a broadcast to a group answers with when the group has no recipient. A group the caller may not
    /// see is answered with exactly this text, so it cannot be told apart from an empty or missing group.
    /// </summary>
    public const string BroadcastGroupEmptyError = "Group is empty";

    /// <summary>
    /// Error a broadcast to id numbers answers with when none resolves to a client; hidden clients count as
    /// unresolved, so the answer never reveals that a hidden id number exists.
    /// </summary>
    public const string BroadcastNoClientsForIdNumbersError = "No clients found for the given id numbers";

    /// <summary>
    /// How many raw pages the message list reads at most to fill one page with messages the caller may see.
    /// Bounds the cost for a caller whose visible messages are rare among all stored ones.
    /// </summary>
    public const int MaxVisibleMessagePageScans = 5;

    /// <summary>
    /// Error the send skill answers a non-admin caller with when it asks for the owner or an application user, the
    /// two recipients the messaging UI and REST routes offer to administrators only.
    /// </summary>
    public const string SendRequiresAdministratorError =
        "Sending to the owner or to application users requires administrator rights";

    /// <summary>
    /// Error format the send skill answers with when the owner has no address for the requested provider.
    /// Placeholder 0 is the provider name.
    /// </summary>
    public const string OwnerAddressMissingErrorFormat =
        "No '{0}' identifier is configured for the owner. Open Settings -> Owner-Messenger and add a {0} entry.";

    /// <summary>
    /// Error format the send skill answers with when no contact of the requested messenger type matches the recipient. A
    /// client the caller may not see is answered with exactly this text. Placeholder 0 is the messenger type, 1 the recipient.
    /// </summary>
    public const string ClientContactMissingErrorFormat =
        "No {0} contact found for '{1}'. The client must have a {0} entry in their Messenger tab.";

    /// <summary>
    /// Error format for a provider name that maps to no messenger type. Placeholder 0 is the provider name.
    /// </summary>
    public const string UnknownProviderErrorFormat = "Unknown messaging provider '{0}'.";

    /// <summary>
    /// Rows the client name search reads at most. The search runs before the caller's group visibility is applied, so it
    /// reads well beyond the candidates it shows: hidden clients must not push a visible one out of the result.
    /// </summary>
    public const int ClientNameSearchRowLimit = 50;

    /// <summary>
    /// Candidates an ambiguous client name lists at most, counted after the group visibility filter.
    /// </summary>
    public const int ClientNameCandidateLimit = 5;

    public const string SkillSendMessage = "send_message";

    /// <summary>
    /// Kind of the facts a send_message confirmation is bound to (provider, recipient address, content type and a hash of the
    /// text). The host stores it with the confirmation token and hands it back on the confirmed replay; the skill accepts only
    /// this kind.
    /// </summary>
    public const string MessageBindingKind = "message";

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

    /// <summary>
    /// Bounds for the message list page size; requests outside the bounds are clamped so a caller
    /// cannot pull the whole message store in one query.
    /// </summary>
    public const int MinMessageQueryCount = 1;

    public const int MaxMessageQueryCount = 200;

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

    /// <summary>
    /// Sender display name recorded for every message sent via SendMessageSkill, so the messaging UI
    /// can show who actually wrote an outbound message instead of the raw recipient identifier.
    /// </summary>
    public const string KlacksySenderDisplayName = "Klacksy";
}
