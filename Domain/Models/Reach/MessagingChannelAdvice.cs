// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Counted reach of one provider. Carries no address.
/// </summary>
/// <param name="Provider">Provider name</param>
/// <param name="IsEnabled">False for a provider that is switched off</param>
/// <param name="CanReceiveReplies">False for a send-only channel</param>
/// <param name="ViaContact">People addressed through a messenger contact</param>
/// <param name="ViaPhone">People addressed through the mobile number fallback</param>
/// <param name="Reachable">Sum of both</param>

namespace Klacks.Plugin.Messaging.Domain.Models.Reach;

public sealed record MessagingChannelAdvice(
    string Provider,
    bool IsEnabled,
    bool CanReceiveReplies,
    int ViaContact,
    int ViaPhone,
    int Reachable);
