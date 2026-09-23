// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Single rule for which Slack channel the inbound poller reads, shared by the Slack provider and the
/// setup diagnosis so both always agree on polling versus webhook mode. conversations.history only
/// accepts a channel ID; a '#name' in DefaultChannel would need channels:read to resolve, which the
/// send-only scope set does not include, so such a value never enables polling.
/// </summary>
/// <param name="channelId">Explicit Slack channel ID from the provider configuration</param>
/// <param name="defaultChannel">Default send channel from the provider configuration, an ID or a '#name'</param>
namespace Klacks.Plugin.Messaging.Application.Services;

public static class SlackPollingChannel
{
    private const char ChannelNamePrefix = '#';

    public static string? Resolve(string? channelId, string? defaultChannel)
    {
        if (!string.IsNullOrWhiteSpace(channelId))
            return channelId.Trim();

        var fallback = defaultChannel?.Trim() ?? string.Empty;
        return fallback.Length > 0 && fallback[0] != ChannelNamePrefix ? fallback : null;
    }
}
