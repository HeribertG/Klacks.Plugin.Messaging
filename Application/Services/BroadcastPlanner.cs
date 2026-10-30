// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Computes a send_broadcast plan in the order the send refuses: provider, audience, addresses. A provider that is missing or
/// disabled, an empty audience and an audience without any address fail here, so the preview issues no token for a call the send
/// would refuse. Recipients whose delivery of exactly this text on this provider is recorded within the repeat window are left out,
/// so a run after a finished run reaches only those who did not get it, and when nobody is left the plan fails and nothing goes out.
/// The delivery claims each recipient with a committed row before the send, so an interrupted run is seen too; two runs side by side
/// are kept apart by IBroadcastRunLock, which send_broadcast takes before it plans. It only reads.
/// </summary>
/// <param name="audienceResolver">Clients of the audience under the caller's rights</param>
/// <param name="messagingService">Resolves the addresses on an enabled provider</param>
/// <param name="providerRepository">Enabled providers, for the sole-provider default</param>
/// <param name="messageRepository">Broadcasts already delivered within the repeat window</param>

using System.Globalization;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public class BroadcastPlanner : IBroadcastPlanner
{
    private readonly IBroadcastAudienceResolver _audienceResolver;
    private readonly IMessagingService _messagingService;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessageRepository _messageRepository;

    public BroadcastPlanner(
        IBroadcastAudienceResolver audienceResolver,
        IMessagingService messagingService,
        IMessagingProviderRepository providerRepository,
        IMessageRepository messageRepository)
    {
        _audienceResolver = audienceResolver;
        _messagingService = messagingService;
        _providerRepository = providerRepository;
        _messageRepository = messageRepository;
    }

    public async Task<BroadcastPlan> PlanAsync(SendBroadcastParameters parameters, bool isAdmin, CancellationToken ct = default)
    {
        var provider = parameters.Provider;
        if (provider == null)
        {
            var sole = await SoleMessagingProvider.ResolveAsync(_providerRepository);
            if (sole.Error != null)
            {
                return BroadcastPlan.Failed(sole.Error);
            }

            provider = sole.ProviderType!;
        }

        var audience = await _audienceResolver.ResolveAsync(parameters.Audience, isAdmin, ct);
        if (audience.ClientIds.Count == 0)
        {
            return BroadcastPlan.Failed(audience.EmptyError);
        }

        BroadcastRecipientSet resolved;
        try
        {
            resolved = await _messagingService.ResolveBroadcastRecipientsAsync(provider, audience.ClientIds, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BroadcastPlan.Failed(ex.Message);
        }

        if (resolved.Recipients.Count == 0)
        {
            return BroadcastPlan.Failed(string.Format(CultureInfo.InvariantCulture, MessagingConstants.BroadcastNoRecipientsErrorFormat, provider));
        }

        var since = DateTime.UtcNow.AddMinutes(-MessagingConstants.BroadcastRepeatWindowMinutes);
        var delivered = await _messageRepository.GetRecentBroadcastClientIdsAsync(resolved.ProviderId, parameters.Content, since, ct);
        var remaining = resolved.Recipients.Where(recipient => !delivered.Contains(recipient.ClientId)).ToList();
        if (remaining.Count == 0)
        {
            return BroadcastPlan.Failed(MessagingConstants.BroadcastAlreadyDeliveredError);
        }

        return BroadcastPlan.Ready(resolved with { Recipients = remaining }, resolved.Recipients.Count - remaining.Count);
    }
}
