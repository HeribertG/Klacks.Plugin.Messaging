// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Reach of an audience across all channels: the counted facts, the findings, the names of the people no enabled channel reaches
/// and the computed recommendation. Carries no phone number and no chat id.
/// </summary>
/// <param name="AudienceCount">People the caller may address</param>
/// <param name="ReachableCount">People at least one enabled channel reaches</param>
/// <param name="ReachableWithReplyCount">People at least one enabled two-way channel reaches</param>
/// <param name="SendOnlyCount">People reachable, but only on channels where they cannot answer</param>
/// <param name="MissingCount">People no enabled channel reaches</param>
/// <param name="BestChannel">The enabled channel that reaches most people; null when none reaches anybody</param>
/// <param name="Channels">Reach per provider</param>
/// <param name="Findings">The findings in a stable order</param>
/// <param name="MissingNames">Names of missing people, capped at MessagingConstants.MaxReachMissingNames</param>
/// <param name="Recommendation">The computed recommendation, to be relayed unchanged</param>

namespace Klacks.Plugin.Messaging.Domain.Models.Reach;

public sealed record MessagingReachAdvice(
    int AudienceCount,
    int ReachableCount,
    int ReachableWithReplyCount,
    int SendOnlyCount,
    int MissingCount,
    string? BestChannel,
    IReadOnlyList<MessagingChannelAdvice> Channels,
    IReadOnlyList<MessagingReachFinding> Findings,
    IReadOnlyList<string> MissingNames,
    string Recommendation);
