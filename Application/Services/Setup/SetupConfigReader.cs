// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Reads values from a provider's ConfigJson for the setup diagnosis, matching keys case-insensitively the
/// same way the provider config records deserialize them (last duplicate wins). Uses JsonDocument, which
/// tolerates duplicate keys; malformed JSON reads as empty.
/// </summary>
/// <param name="configJson">Provider-specific configuration JSON</param>
/// <param name="key">Configuration key to read</param>
/// <param name="keys">Keys whose values are all collected, including every duplicate under any casing</param>
using System.Text.Json;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public static class SetupConfigReader
{
    public static string? GetString(string? configJson, string key)
    {
        return ReadScalarProperties(configJson)
            .LastOrDefault(property => string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    public static IReadOnlyList<string> GetAllValuesOf(string? configJson, IReadOnlyCollection<string> keys)
    {
        return ReadScalarProperties(configJson)
            .Where(property => keys.Contains(property.Key, StringComparer.OrdinalIgnoreCase))
            .Select(property => property.Value)
            .ToList();
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ReadScalarProperties(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return [];

        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return [];

            return document.RootElement.EnumerateObject()
                .Select(property => new KeyValuePair<string, string>(property.Name, ScalarText(property.Value) ?? string.Empty))
                .Where(property => !string.IsNullOrWhiteSpace(property.Value))
                .Select(property => new KeyValuePair<string, string>(property.Key, property.Value.Trim()))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ScalarText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };
    }
}
