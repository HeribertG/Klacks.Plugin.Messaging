// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Aggregated result of a broadcast send operation across all recipients of a group.
/// </summary>
/// <param name="BroadcastId">Identifier shared by all Message rows belonging to this broadcast</param>
/// <param name="Total">Total number of clients in the target group</param>
/// <param name="Sent">Number of recipients the provider accepted</param>
/// <param name="Failed">Number of recipients where the provider returned an error</param>
/// <param name="SkippedNoContact">Number of clients without an explicit MessengerContact and no usable phone fallback</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public record BroadcastSendResult(
    Guid BroadcastId,
    int Total,
    int Sent,
    int Failed,
    int SkippedNoContact);
