// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Single backend definition of the required configuration keys per provider type, so the setup
/// diagnosis check cannot drift from what each provider's own config record actually deserializes.
/// Config keys are resolved case-insensitively the same way System.Text.Json's own
/// PropertyNameCaseInsensitive deserializer resolves them: when a JSON object carries the same key
/// under more than one casing, the value of the LAST matching property wins, in document order.
/// </summary>
using System.Text.Json;
using System.Text.Json.Nodes;
using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public static class ProviderRequiredConfigFields
{
    private static readonly IReadOnlyDictionary<string, string[]> Required =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [MessagingConstants.ProviderTelegram] = ["BotToken"],
            [MessagingConstants.ProviderSlack] = ["BotToken"],
            [MessagingConstants.ProviderWhatsApp] = ["AccessToken", "PhoneNumberId", "AppSecret", "VerifyToken"],
            [MessagingConstants.ProviderViber] = ["AuthToken"],
            [MessagingConstants.ProviderLine] = ["ChannelAccessToken", "ChannelSecret"],
            [MessagingConstants.ProviderSignal] = ["SignalNumber", "ApiUrl"],
            [MessagingConstants.ProviderSms] = ["AccountSid", "AuthToken", "SenderNumber"],
            [MessagingConstants.ProviderThreema] = ["GatewayId", "ApiSecret"],
            [MessagingConstants.ProviderKakaoTalk] = ["AccessToken"],
            [MessagingConstants.ProviderWeChat] = ["AppId", "AppSecret"],
            [MessagingConstants.ProviderZalo] = ["AccessToken"],
            [MessagingConstants.ProviderTeams] = ["WebhookUrl"],
        };

    public static IReadOnlyList<string> GetMissing(string providerType, string? configJson)
    {
        if (!Required.TryGetValue(providerType, out var keys))
            return [];

        var config = TryParse(configJson);
        return keys.Where(key => !HasValue(config, key)).ToList();
    }

    private static bool HasValue(JsonObject? config, string key)
    {
        if (config == null)
            return false;

        JsonValue? resolved = null;
        foreach (var property in config)
        {
            if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) && property.Value is JsonValue value)
                resolved = value;
        }

        return resolved != null && !string.IsNullOrWhiteSpace(resolved.ToString());
    }

    private static JsonObject? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var config = JsonNode.Parse(json) as JsonObject;

            // JsonObject builds its backing dictionary lazily on first enumeration, so a duplicate key
            // (even one differing only in case) does not throw here yet - force that materialization
            // now, inside this try, instead of leaving it to surprise the caller on first use.
            _ = config?.Count;

            return config;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
