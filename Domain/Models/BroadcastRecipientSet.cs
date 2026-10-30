// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The resolved addresses of one broadcast on one enabled provider, as they will be sent to.
/// </summary>
/// <param name="ProviderId">The enabled provider</param>
/// <param name="ProviderName">Its name, the name the send path looks it up by</param>
/// <param name="Recipients">Clients with a usable address</param>
/// <param name="Skipped">Clients of the audience without a usable address</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public sealed record BroadcastRecipientSet(
    Guid ProviderId,
    string ProviderName,
    IReadOnlyList<BroadcastRecipient> Recipients,
    int Skipped);
