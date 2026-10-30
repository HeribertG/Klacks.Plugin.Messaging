// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Collects the facts of evaluate_messaging_reach and returns the computed advice. It only reads.
/// </summary>

using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Models.Reach;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessagingReachService
{
    Task<MessagingReachAdvice> EvaluateAsync(BroadcastAudienceParameters parameters, bool isAdmin, CancellationToken ct = default);
}
