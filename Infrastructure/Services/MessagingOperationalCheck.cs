// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Checks whether the messaging plugin is operational by verifying at least one enabled provider exists.
/// </summary>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class MessagingOperationalCheck : IPluginOperationalCheck
{
    private readonly IMessagingProviderRepository _providerRepository;

    public MessagingOperationalCheck(IMessagingProviderRepository providerRepository)
    {
        _providerRepository = providerRepository;
    }

    public string PluginName => MessagingConstants.PluginName;

    public async Task<bool> IsOperationalAsync()
    {
        var enabledProviders = await _providerRepository.GetEnabledAsync();
        return enabledProviders.Count > 0;
    }
}
