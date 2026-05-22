// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Optional interface for messaging provider adapters that support webhook self-registration.
/// Implemented only by providers that can call their own platform API to register a webhook URL.
/// </summary>
/// <param name="configJson">Provider-specific configuration JSON containing the bot token and webhook URL</param>
/// <param name="webhookSecret">Secret token sent by the platform with each webhook delivery</param>
namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IWebhookRegistrar
{
    Task<bool> RegisterWebhookAsync(string configJson, string webhookSecret, CancellationToken ct = default);
}
