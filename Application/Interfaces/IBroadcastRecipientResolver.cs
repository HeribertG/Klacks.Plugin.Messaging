// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The one rule that turns clients into broadcast addresses: the messenger contact of the provider's type, otherwise the mobile
/// number where the provider accepts one. Throws InvalidOperationException for a provider type no messenger type matches.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IBroadcastRecipientResolver
{
    Task<(IReadOnlyList<BroadcastRecipient> Recipients, int Skipped)> ResolveAsync(
        string providerType, bool supportsPhoneAsRecipient, IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default);
}
