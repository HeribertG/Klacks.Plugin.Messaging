// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

/// <summary>
/// Lets one broadcast run at a time across every server process on this database, so two runs cannot both pass the repeat check
/// and send the same text. The lock covers all providers, because the planner resolves the provider only after it has been taken.
/// Within one request it is re-entrant: send_broadcast holds it around plan and send, and the delivery, which also serves the
/// HTTP routes, takes it again without being refused.
/// </summary>
public interface IBroadcastRunLock
{
    /// <summary>
    /// Takes the lock without waiting. Returns null when another run holds it; a caller then sends nothing and answers with
    /// <see cref="MessagingConstants.BroadcastAlreadyRunningError"/>. Disposing the returned handle releases it.
    /// </summary>
    /// <param name="ct">Cancels taking the lock</param>
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct);
}
