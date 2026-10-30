// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Everything the reach advice is computed from.
/// </summary>
/// <param name="Audience">Clients the caller may address, in a stable order</param>
/// <param name="Channels">Every configured provider</param>

namespace Klacks.Plugin.Messaging.Domain.Models.Reach;

public sealed record MessagingReachFacts(IReadOnlyList<Guid> Audience, IReadOnlyList<ChannelReach> Channels);
