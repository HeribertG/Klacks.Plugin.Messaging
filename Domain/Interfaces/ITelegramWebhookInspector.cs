// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Reads the webhook state Telegram holds for the bot (getWebhookInfo), including the last delivery error.
/// </summary>
/// <param name="configJson">Telegram provider configuration JSON containing the bot token</param>
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface ITelegramWebhookInspector
{
    Task<TelegramWebhookInfo?> GetWebhookInfoAsync(string configJson, CancellationToken ct = default);
}
