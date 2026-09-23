// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// The configuration keys per provider type whose values are credentials and must never reach the setup
/// report. Identifiers such as PhoneNumberId or AccountSid stay readable. For Microsoft Teams the
/// WebhookUrl itself is the credential; for every other type it is Klacks' own public endpoint.
/// </summary>
/// <param name="providerType">Provider type constant from MessagingConstants</param>
using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public static class ProviderSecretConfigKeys
{
    private static readonly IReadOnlyDictionary<string, string[]> SecretKeys =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [MessagingConstants.ProviderTelegram] = ["BotToken"],
            [MessagingConstants.ProviderSlack] = ["BotToken", SetupConfigKeys.SigningSecret],
            [MessagingConstants.ProviderWhatsApp] = ["AccessToken", "AppSecret", "VerifyToken"],
            [MessagingConstants.ProviderViber] = ["AuthToken"],
            [MessagingConstants.ProviderLine] = ["ChannelAccessToken", "ChannelSecret"],
            [MessagingConstants.ProviderSignal] = [],
            [MessagingConstants.ProviderSms] = ["AuthToken"],
            [MessagingConstants.ProviderThreema] = ["ApiSecret"],
            [MessagingConstants.ProviderKakaoTalk] = ["AccessToken"],
            [MessagingConstants.ProviderWeChat] = ["AppSecret"],
            [MessagingConstants.ProviderZalo] = ["AccessToken"],
            [MessagingConstants.ProviderTeams] = [SetupConfigKeys.WebhookUrl],
        };

    public static IReadOnlyCollection<string> For(string providerType) =>
        SecretKeys.TryGetValue(providerType, out var keys) ? keys : [];
}
