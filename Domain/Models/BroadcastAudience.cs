// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The clients a broadcast is meant for, narrowed to what the caller may address.
/// </summary>
/// <param name="ClientIds">Distinct clients of the audience</param>
/// <param name="EmptyError">Refusal for an empty audience; the same text for a hidden, an empty and an unknown audience</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public sealed record BroadcastAudience(IReadOnlyList<Guid> ClientIds, string EmptyError);
