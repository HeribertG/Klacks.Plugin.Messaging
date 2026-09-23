// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Checks whether inbound messages arrive (stored message, discarded unknown sender, failed signature
/// validation) and whether the owner has a messenger identity of this type. Unknown senders are reported
/// as "recent unknown sender(s)" with only the most recent one as candidate, never as the admin.
/// Sender display names are attacker-controlled text that reaches the LLM, so control, format (bidi,
/// zero-width, tag characters), private-use, unassigned and separator code points are removed and the
/// name is capped by code points; the sender id is reported unchanged.
/// </summary>
/// <param name="ownerReader">Reads the owner messenger identities (APP_OWNER_MESSENGERS)</param>
using System.Globalization;
using System.Text;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class InboundStepChecker
{
    private static readonly HashSet<UnicodeCategory> InvisibleCategories =
    [
        UnicodeCategory.Control,
        UnicodeCategory.Format,
        UnicodeCategory.PrivateUse,
        UnicodeCategory.OtherNotAssigned,
        UnicodeCategory.Surrogate,
        UnicodeCategory.LineSeparator,
        UnicodeCategory.ParagraphSeparator
    ];

    private readonly IOwnerMessengerReader _ownerReader;

    public InboundStepChecker(IOwnerMessengerReader ownerReader)
    {
        _ownerReader = ownerReader;
    }

    public SetupStep CheckInbound(ProviderDiagnosisContext context)
    {
        if (context.LatestInboundAtUtc != null)
            return new SetupStep(SetupStepCodes.InboundObserved, SetupStepStatus.Ok);

        var activity = context.Activity;
        var candidateFacts = MostRecentUnknownSenderFacts(activity);
        if (candidateFacts != null)
            return new SetupStep(SetupStepCodes.InboundObserved, SetupStepStatus.ActionRequired, SetupStepDetails.RecentUnknownSenders, candidateFacts);

        if (activity.LastSignatureRejectionUtc != null
            && (activity.LastWebhookHitUtc == null || activity.LastSignatureRejectionUtc > activity.LastWebhookHitUtc))
        {
            return new SetupStep(SetupStepCodes.InboundObserved, SetupStepStatus.Error, SetupStepDetails.SignatureRejected);
        }

        return activity.LastWebhookHitUtc != null
            ? new SetupStep(SetupStepCodes.InboundObserved, SetupStepStatus.Ok)
            : new SetupStep(SetupStepCodes.InboundObserved, SetupStepStatus.ActionRequired, SetupStepDetails.NoInboundYet);
    }

    public async Task<SetupStep> CheckOwnerAsync(ProviderDiagnosisContext context, CancellationToken ct)
    {
        if (context.MessengerType == null)
            return new SetupStep(SetupStepCodes.OwnerIdentity, SetupStepStatus.NotChecked);

        var owner = await _ownerReader.GetByTypeAsync(context.MessengerType.Value, ct);
        if (owner != null)
            return new SetupStep(SetupStepCodes.OwnerIdentity, SetupStepStatus.Ok);

        var candidateFacts = MostRecentUnknownSenderFacts(context.Activity);
        return candidateFacts == null
            ? new SetupStep(SetupStepCodes.OwnerIdentity, SetupStepStatus.ActionRequired, SetupStepDetails.NoOwnerIdentity)
            : new SetupStep(SetupStepCodes.OwnerIdentity, SetupStepStatus.ActionRequired, SetupStepDetails.NoOwnerIdentityWithUnknownSenders, candidateFacts);
    }

    private static IReadOnlyDictionary<string, string>? MostRecentUnknownSenderFacts(InboundActivitySnapshot activity)
    {
        var mostRecent = activity.UnknownSenders.MaxBy(sighting => sighting.SeenAtUtc);
        if (mostRecent == null)
            return null;

        var facts = new Dictionary<string, string>
        {
            [MessagingSetupConstants.FactUnknownSenderId] = mostRecent.SenderId
        };

        var displayName = SanitizeDisplayName(mostRecent.DisplayName);
        if (displayName != null)
            facts[MessagingSetupConstants.FactUnknownSenderName] = displayName;

        return facts;
    }

    private static string? SanitizeDisplayName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
            return null;

        var visible = displayName.EnumerateRunes()
            .Where(rune => !InvisibleCategories.Contains(Rune.GetUnicodeCategory(rune)))
            .ToList();

        var printable = string.Concat(visible.Take(MessagingSetupConstants.MaxUnknownSenderNameLength).Select(rune => rune.ToString())).Trim();
        return printable.Length == 0 ? null : printable;
    }
}
