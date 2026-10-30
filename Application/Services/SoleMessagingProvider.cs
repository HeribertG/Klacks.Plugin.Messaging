// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Picks the provider when a skill call names none: with exactly one enabled provider there is nothing to ask about. Returns the
/// provider's type, not its admin-assigned name, because that is what both the messenger mapping and the provider lookup take.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Application.Services;

public static class SoleMessagingProvider
{
    public static async Task<(string? ProviderType, string? Error)> ResolveAsync(IMessagingProviderRepository providerRepository)
    {
        var enabledProviders = await providerRepository.GetEnabledAsync();

        if (enabledProviders.Count == 0)
        {
            return (null, "No messaging provider is configured and enabled. An admin must set one up under Settings -> Messaging.");
        }

        if (enabledProviders.Count > 1)
        {
            var names = string.Join(", ", enabledProviders.Select(p => p.Name));
            return (null, $"The 'provider' parameter is required: multiple messaging providers are enabled ({names}). Specify which one to use.");
        }

        return (enabledProviders[0].ProviderType, null);
    }
}
