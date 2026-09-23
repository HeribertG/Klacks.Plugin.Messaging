// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Which setup steps apply to which provider type: the five inbound-capable providers, the providers
/// that need a public webhook URL, and the Slack polling/webhook mode switch. The Slack mode uses the
/// same SlackPollingChannel rule as the provider's poller, so the diagnosis cannot drift from it.
/// </summary>
/// <param name="providerType">Provider type constant from MessagingConstants</param>
/// <param name="configJson">Provider configuration JSON, used to detect the Slack mode</param>
using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public static class SetupProviderCatalog
{
    private static readonly HashSet<string> InboundCapableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        MessagingConstants.ProviderTelegram,
        MessagingConstants.ProviderSlack,
        MessagingConstants.ProviderWhatsApp,
        MessagingConstants.ProviderViber,
        MessagingConstants.ProviderLine,
    };

    private static readonly HashSet<string> AlwaysWebhookTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        MessagingConstants.ProviderTelegram,
        MessagingConstants.ProviderWhatsApp,
        MessagingConstants.ProviderViber,
        MessagingConstants.ProviderLine,
    };

    public static bool IsInboundCapable(string providerType) => InboundCapableTypes.Contains(providerType);

    public static bool Is(string providerType, string expectedType) =>
        string.Equals(providerType, expectedType, StringComparison.OrdinalIgnoreCase);

    public static bool IsSlackWebhookMode(string providerType, string? configJson) =>
        Is(providerType, MessagingConstants.ProviderSlack)
        && SlackPollingChannel.Resolve(
            SetupConfigReader.GetString(configJson, SetupConfigKeys.ChannelId),
            SetupConfigReader.GetString(configJson, SetupConfigKeys.DefaultChannel)) == null;

    public static bool NeedsWebhook(string providerType, string? configJson) =>
        AlwaysWebhookTypes.Contains(providerType) || IsSlackWebhookMode(providerType, configJson);
}
