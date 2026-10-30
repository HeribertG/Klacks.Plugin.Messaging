// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// What one send_broadcast call would do, computed the same way for the confirmation preview and for the confirmed send.
/// </summary>
/// <param name="Error">Why the call cannot run; null when it can</param>
/// <param name="Recipients">The addresses that would receive the text; null when the call cannot run</param>
/// <param name="AlreadyReceived">Clients left out because they received exactly this text within the repeat window</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public sealed record BroadcastPlan(string? Error, BroadcastRecipientSet? Recipients, int AlreadyReceived)
{
    public bool Success => Error == null;

    public static BroadcastPlan Failed(string error) => new(error, null, 0);

    public static BroadcastPlan Ready(BroadcastRecipientSet recipients, int alreadyReceived) => new(null, recipients, alreadyReceived);
}
