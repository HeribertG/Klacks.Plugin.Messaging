// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Compares the items of a confirmed binding with the items computed right before acting, whatever order the host stored them in.
/// An empty confirmed list never matches: the host keeps no items above its storage cap, and nothing is proven then.
/// </summary>

namespace Klacks.Plugin.Messaging.Application.Services;

public static class ConfirmedBindingItems
{
    public static bool AreEqual(IReadOnlyList<string> confirmed, IReadOnlyList<string> current) =>
        confirmed.Count > 0
        && confirmed.OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(current.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal);
}
