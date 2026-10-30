// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Collects the facts of the reach advice: the audience under the caller's rights, then per configured provider the addresses the
/// broadcast rule would resolve, for a switched-off provider too, so the advice can say whom it would reach. Names are read only
/// for the people no enabled channel reaches, capped, and only among the audience the caller may see. A provider whose type maps
/// to no messenger cannot address anybody and is left out. It only reads.
/// </summary>
/// <param name="audienceResolver">Clients of the audience under the caller's rights</param>
/// <param name="providerRepository">Every configured provider</param>
/// <param name="adapterFactory">What a provider's adapter accepts</param>
/// <param name="recipientResolver">The broadcast address rule</param>
/// <param name="contactRepository">Display names of the missing people</param>

using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Reach;

namespace Klacks.Plugin.Messaging.Application.Services;

public class MessagingReachService : IMessagingReachService
{
    private readonly IBroadcastAudienceResolver _audienceResolver;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessagingProviderAdapterFactory _adapterFactory;
    private readonly IBroadcastRecipientResolver _recipientResolver;
    private readonly IMessengerContactRepository _contactRepository;

    public MessagingReachService(
        IBroadcastAudienceResolver audienceResolver,
        IMessagingProviderRepository providerRepository,
        IMessagingProviderAdapterFactory adapterFactory,
        IBroadcastRecipientResolver recipientResolver,
        IMessengerContactRepository contactRepository)
    {
        _audienceResolver = audienceResolver;
        _providerRepository = providerRepository;
        _adapterFactory = adapterFactory;
        _recipientResolver = recipientResolver;
        _contactRepository = contactRepository;
    }

    public async Task<MessagingReachAdvice> EvaluateAsync(BroadcastAudienceParameters parameters, bool isAdmin, CancellationToken ct = default)
    {
        var audience = await _audienceResolver.ResolveAsync(parameters, isAdmin, ct);
        var channels = new List<ChannelReach>();

        if (audience.ClientIds.Count > 0)
        {
            foreach (var provider in await _providerRepository.GetAllAsync())
            {
                if (!Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out _))
                {
                    continue;
                }

                var adapter = _adapterFactory.Create(provider.ProviderType);
                var (recipients, _) = await _recipientResolver.ResolveAsync(
                    provider.ProviderType, adapter.SupportsPhoneAsRecipient, audience.ClientIds, ct);

                channels.Add(new ChannelReach(
                    provider.Name,
                    provider.IsEnabled,
                    SetupProviderCatalog.IsInboundCapable(provider.ProviderType),
                    recipients.Where(recipient => !recipient.FromPhoneFallback).Select(recipient => recipient.ClientId).ToHashSet(),
                    recipients.Where(recipient => recipient.FromPhoneFallback).Select(recipient => recipient.ClientId).ToHashSet()));
            }
        }

        var facts = new MessagingReachFacts(audience.ClientIds, channels);
        var missing = MessagingReachAdvisor.MissingClientIds(facts);
        var names = missing.Count == 0
            ? []
            : await _contactRepository.GetClientDisplayNamesAsync(missing, MessagingConstants.MaxReachMissingNames, ct);

        return MessagingReachAdvisor.Evaluate(facts, names);
    }
}
