// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Whom one configured provider could address in an audience, as resolved by the broadcast recipient rule.
/// </summary>
/// <param name="Provider">Provider name</param>
/// <param name="IsEnabled">False for a provider that is switched off</param>
/// <param name="CanReceiveReplies">False for a send-only channel: an answer can never arrive</param>
/// <param name="ViaContact">Clients with a messenger contact of this provider's type</param>
/// <param name="ViaPhone">Clients reached through the mobile number fallback</param>

namespace Klacks.Plugin.Messaging.Domain.Models.Reach;

public sealed record ChannelReach(
    string Provider,
    bool IsEnabled,
    bool CanReceiveReplies,
    IReadOnlySet<Guid> ViaContact,
    IReadOnlySet<Guid> ViaPhone);
