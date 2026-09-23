// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Singleton, memory-only record of inbound webhook activity per provider: last webhook hit, last
/// signature rejection, and the most recent unknown senders. Lost on restart by design - commissioning
/// is a live activity, and a sighting surviving from a previous process would mislead the admin about
/// what is happening right now.
/// </summary>
using System.Collections.Concurrent;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class MessagingInboundActivityTracker : IMessagingInboundActivityTracker
{
    private readonly Func<DateTime> _utcNow;
    private readonly ConcurrentDictionary<Guid, ProviderActivity> _activityByProvider = new();

    public MessagingInboundActivityTracker()
        : this(() => DateTime.UtcNow)
    {
    }

    public MessagingInboundActivityTracker(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    public void RecordWebhookHit(Guid providerId)
    {
        var activity = GetOrAddActivity(providerId);
        var timestamp = _utcNow();

        lock (activity.Lock)
        {
            activity.LastWebhookHitUtc = timestamp;
        }
    }

    public void RecordSignatureRejection(Guid providerId)
    {
        var activity = GetOrAddActivity(providerId);
        var timestamp = _utcNow();

        lock (activity.Lock)
        {
            activity.LastSignatureRejectionUtc = timestamp;
        }
    }

    public void RecordUnknownSender(Guid providerId, string senderId, string? displayName)
    {
        var activity = GetOrAddActivity(providerId);
        var sighting = new UnknownSenderSighting(senderId, VendorText.Truncate(displayName), _utcNow());

        lock (activity.Lock)
        {
            activity.UnknownSenders.RemoveAll(existing => string.Equals(existing.SenderId, senderId, StringComparison.Ordinal));
            activity.UnknownSenders.Insert(0, sighting);

            if (activity.UnknownSenders.Count > MessagingSetupConstants.MaxUnknownSendersPerProvider)
            {
                activity.UnknownSenders.RemoveRange(
                    MessagingSetupConstants.MaxUnknownSendersPerProvider,
                    activity.UnknownSenders.Count - MessagingSetupConstants.MaxUnknownSendersPerProvider);
            }
        }
    }

    public InboundActivitySnapshot GetSnapshot(Guid providerId)
    {
        if (!_activityByProvider.TryGetValue(providerId, out var activity))
            return new InboundActivitySnapshot(null, null, []);

        var ttlCutoffUtc = _utcNow().AddHours(-MessagingSetupConstants.UnknownSenderTtlHours);

        DateTime? lastWebhookHitUtc;
        DateTime? lastSignatureRejectionUtc;
        List<UnknownSenderSighting> unknownSenders;
        lock (activity.Lock)
        {
            lastWebhookHitUtc = activity.LastWebhookHitUtc;
            lastSignatureRejectionUtc = activity.LastSignatureRejectionUtc;
            unknownSenders = activity.UnknownSenders.Where(sighting => sighting.SeenAtUtc >= ttlCutoffUtc).ToList();
        }

        return new InboundActivitySnapshot(lastWebhookHitUtc, lastSignatureRejectionUtc, unknownSenders);
    }

    private ProviderActivity GetOrAddActivity(Guid providerId)
    {
        return _activityByProvider.GetOrAdd(providerId, static _ => new ProviderActivity());
    }

    private sealed class ProviderActivity
    {
        public object Lock { get; } = new();

        public DateTime? LastWebhookHitUtc { get; set; }

        public DateTime? LastSignatureRejectionUtc { get; set; }

        public List<UnknownSenderSighting> UnknownSenders { get; } = [];
    }
}
