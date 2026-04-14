// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Retrieves metadata about the configured Telegram bot (e.g. username for deep-link construction).
/// Abstraction over the concrete provider so Application-layer services avoid depending on Infrastructure.
/// </summary>

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface ITelegramBotMetadataProvider
{
    /// <summary>
    /// Resolves the bot's public username (without @) by calling Telegram's getMe endpoint.
    /// Cached per bot token. Returns null when the token is invalid or the call fails.
    /// </summary>
    /// <param name="configJson">Telegram provider config JSON holding the bot token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> GetBotUsernameAsync(string configJson, CancellationToken ct = default);
}
