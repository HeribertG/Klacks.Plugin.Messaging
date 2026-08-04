// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Implemented by provider adapters that can fetch inbound messages by polling instead of
/// receiving them through a webhook. A webhook needs a publicly reachable URL; an installation
/// running on a workstation or behind NAT has none, so for those the poller is the only way
/// inbound traffic ever arrives.
/// </summary>
/// <param name="configJson">Provider-specific configuration JSON.</param>
/// <param name="cursor">Provider-specific position of the last round, or null on the first ever run.</param>

using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IInboundMessagePoller
{
    Task<InboundPollResult> PollAsync(string configJson, string? cursor, CancellationToken ct = default);
}
