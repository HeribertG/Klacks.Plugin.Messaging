// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Computes the reach advice from counted facts, deterministically and without any dependency: who is reachable on which enabled
/// channel, who sits only on send-only channels, who a switched-off provider would reach, who has no address at all. A channel
/// counts as reaching a client through the facts the broadcast address rule produced, so the numbers equal what a send would do.
/// An empty audience never says why it is empty: for a non-admin a hidden group must look like an empty one.
/// </summary>

using System.Globalization;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Models.Reach;

namespace Klacks.Plugin.Messaging.Application.Services;

public static class MessagingReachAdvisor
{
    private const string NoVisibleClientsAdvice =
        "There is nobody to reach: this selection has no member you can see.";
    private const string NoEnabledChannelAdvice =
        "No messenger channel is switched on, so nobody can be reached. An administrator has to enable one first.";
    private const string NobodyReachableAdvice =
        "None of the {0} people has an address on an enabled channel.";
    private const string BestChannelAdvice = "{0} reaches {1} of {2} people.";
    private const string SecondChannelAdvice =
        " One send uses one channel, so the other {0} reachable people need a second send on another channel.";
    private const string MissingAdvice = " {0} people cannot be reached on any enabled channel.";
    private const string SendOnlyAdvice = " {0} is send-only: recipients cannot answer there.";

    public static IReadOnlyList<Guid> MissingClientIds(MessagingReachFacts facts)
    {
        var reachable = ReachableOn(facts.Channels.Where(channel => channel.IsEnabled));
        return facts.Audience.Where(clientId => !reachable.Contains(clientId)).ToList();
    }

    public static MessagingReachAdvice Evaluate(MessagingReachFacts facts, IReadOnlyList<string> missingNames)
    {
        var enabled = facts.Channels.Where(channel => channel.IsEnabled).ToList();
        var reachable = ReachableOn(enabled);
        var withReply = ReachableOn(enabled.Where(channel => channel.CanReceiveReplies));
        var missing = MissingClientIds(facts).ToHashSet();
        var sendOnlyCount = reachable.Count - withReply.Count;

        var channels = facts.Channels
            .OrderBy(channel => channel.Provider, StringComparer.Ordinal)
            .Select(channel => new MessagingChannelAdvice(
                channel.Provider,
                channel.IsEnabled,
                channel.CanReceiveReplies,
                channel.ViaContact.Count,
                channel.ViaPhone.Count,
                channel.ViaContact.Count + channel.ViaPhone.Count))
            .ToList();

        var best = channels
            .Where(channel => channel.IsEnabled && channel.Reachable > 0)
            .OrderByDescending(channel => channel.Reachable)
            .ThenByDescending(channel => channel.CanReceiveReplies)
            .ThenBy(channel => channel.Provider, StringComparer.Ordinal)
            .FirstOrDefault();

        return new MessagingReachAdvice(
            facts.Audience.Count,
            reachable.Count,
            withReply.Count,
            sendOnlyCount,
            missing.Count,
            best?.Provider,
            channels,
            BuildFindings(facts, channels, sendOnlyCount, missing),
            missingNames,
            BuildRecommendation(facts.Audience.Count, enabled.Count, best, reachable.Count, missing.Count));
    }

    private static HashSet<Guid> ReachableOn(IEnumerable<ChannelReach> channels) =>
        channels.SelectMany(channel => channel.ViaContact.Concat(channel.ViaPhone)).ToHashSet();

    private static List<MessagingReachFinding> BuildFindings(
        MessagingReachFacts facts, IReadOnlyList<MessagingChannelAdvice> channels, int sendOnlyCount, HashSet<Guid> missing)
    {
        if (facts.Audience.Count == 0)
        {
            return [new MessagingReachFinding(MessagingReachFindingCodes.NoVisibleClients, 0)];
        }

        var findings = channels
            .Where(channel => channel.IsEnabled && channel.Reachable > 0)
            .Select(channel => new MessagingReachFinding(MessagingReachFindingCodes.ReachableViaChannel, channel.Reachable, channel.Provider))
            .ToList();

        if (sendOnlyCount > 0)
        {
            findings.Add(new MessagingReachFinding(MessagingReachFindingCodes.SendOnlyNoReply, sendOnlyCount));
        }

        var coveredByDisabled = new HashSet<Guid>();
        foreach (var channel in facts.Channels.Where(channel => !channel.IsEnabled).OrderBy(channel => channel.Provider, StringComparer.Ordinal))
        {
            var wouldReach = ReachableOn([channel]).Where(missing.Contains).ToList();
            coveredByDisabled.UnionWith(wouldReach);
            if (wouldReach.Count > 0)
            {
                findings.Add(new MessagingReachFinding(MessagingReachFindingCodes.ProviderDisabled, wouldReach.Count, channel.Provider));
            }
        }

        if (!channels.Any(channel => channel.IsEnabled))
        {
            findings.Add(new MessagingReachFinding(MessagingReachFindingCodes.NoEnabledChannel, facts.Audience.Count));
        }

        var withoutMessenger = missing.Count - coveredByDisabled.Count;
        if (withoutMessenger > 0)
        {
            findings.Add(new MessagingReachFinding(MessagingReachFindingCodes.NoMessenger, withoutMessenger));
        }

        return findings;
    }

    private static string BuildRecommendation(int audienceCount, int enabledCount, MessagingChannelAdvice? best, int reachableCount, int missingCount)
    {
        if (audienceCount == 0)
        {
            return NoVisibleClientsAdvice;
        }

        if (enabledCount == 0)
        {
            return NoEnabledChannelAdvice;
        }

        if (best == null)
        {
            return Format(NobodyReachableAdvice, audienceCount);
        }

        var advice = Format(BestChannelAdvice, best.Provider, best.Reachable, audienceCount);
        if (reachableCount > best.Reachable)
        {
            advice += Format(SecondChannelAdvice, reachableCount - best.Reachable);
        }

        if (missingCount > 0)
        {
            advice += Format(MissingAdvice, missingCount);
        }

        if (!best.CanReceiveReplies)
        {
            advice += Format(SendOnlyAdvice, best.Provider);
        }

        return advice;
    }

    private static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, template, values);
}
