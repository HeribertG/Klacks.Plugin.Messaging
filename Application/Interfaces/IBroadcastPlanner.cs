// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Computes what a send_broadcast call would do. The host's confirmation preview and the skill both ask here, so the facts the
/// user confirms and the facts the skill compares before sending come from one path. It only reads.
/// </summary>

using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IBroadcastPlanner
{
    Task<BroadcastPlan> PlanAsync(SendBroadcastParameters parameters, bool isAdmin, CancellationToken ct = default);
}
