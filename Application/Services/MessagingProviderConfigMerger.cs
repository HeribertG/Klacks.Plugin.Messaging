// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Merges an incoming provider configuration into the stored one instead of replacing it.
/// Provider secrets are write-only: MessagingProviderDto deliberately omits ConfigJson, so the
/// client can never send back the values it did not change. A plain assignment therefore wipes
/// credentials whenever a caller submits a partial payload - the enable/disable toggle sends an
/// empty string, and the edit dialog sends only the fields it was able to prefill.
/// Keys present in the incoming payload win; keys absent from it keep their stored value.
/// </summary>
/// <param name="storedJson">The configuration currently persisted for the provider.</param>
/// <param name="incomingJson">The configuration submitted by the client, possibly partial or empty.</param>
/// <param name="merged">The merged configuration, valid only when the method returns true.</param>

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Klacks.Plugin.Messaging.Application.Services;

public static class MessagingProviderConfigMerger
{
    private const string EmptyObject = "{}";

    public static bool TryMerge(string? storedJson, string? incomingJson, out string merged)
    {
        merged = string.IsNullOrWhiteSpace(storedJson) ? EmptyObject : storedJson;

        if (string.IsNullOrWhiteSpace(incomingJson))
        {
            return true;
        }

        if (!TryParseObject(incomingJson, out var incoming))
        {
            return false;
        }

        if (!TryParseObject(merged, out var target))
        {
            target = new JsonObject();
        }

        foreach (var property in incoming!)
        {
            target![property.Key] = property.Value?.DeepClone();
        }

        merged = target!.ToJsonString();
        return true;
    }

    private static bool TryParseObject(string json, out JsonObject? result)
    {
        result = null;

        try
        {
            result = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        return result != null;
    }
}
