// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Context passed to provider adapters for validating an incoming webhook request.
/// </summary>
/// <param name="Body">Raw webhook request body</param>
/// <param name="Headers">All HTTP request headers (case-insensitive lookup via GetHeader)</param>
/// <param name="ConfigJson">Provider-specific configuration JSON containing API keys and secrets</param>
/// <param name="WebhookSecret">Server-generated webhook secret of the provider</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record WebhookValidationContext(
    string Body,
    IReadOnlyDictionary<string, string> Headers,
    string ConfigJson,
    string WebhookSecret)
{
    public string? GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : null;
    }
}
