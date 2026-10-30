// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Resolves the clients of a broadcast audience with the rights of MessagingController: an administrator gets the unrestricted
/// audience, every other caller only the clients of its group visibility.
/// </summary>

using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IBroadcastAudienceResolver
{
    Task<BroadcastAudience> ResolveAsync(BroadcastAudienceParameters parameters, bool isAdmin, CancellationToken ct = default);
}
