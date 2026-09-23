// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Lists the known product limitations that apply to the provider in its current situation, one step per
/// limitation: Slack pairing codes cannot be redeemed, pairing fails with more than one enabled provider,
/// and Telegram onboarding needs a usable webhook.
/// </summary>
/// <param name="context">Per-provider diagnosis state; supplies the enabled provider count and webhook URL outcome</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class KnownLimitationStepChecker
{
    private const int SingleEnabledProvider = 1;

    public IReadOnlyList<SetupStep> Check(ProviderDiagnosisContext context)
    {
        var providerType = context.Provider.ProviderType;
        var limitations = new List<string>();

        if (SetupProviderCatalog.Is(providerType, MessagingConstants.ProviderSlack))
            limitations.Add(MessagingSetupConstants.LimitationSlackPairing);

        if (context.Provider.IsEnabled && context.EnabledProviderCount > SingleEnabledProvider)
            limitations.Add(MessagingSetupConstants.LimitationMultipleProvidersPairing);

        if (SetupProviderCatalog.Is(providerType, MessagingConstants.ProviderTelegram) && context.WebhookUrlStatus != SetupStepStatus.Ok)
            limitations.Add(MessagingSetupConstants.LimitationTelegramNeedsWebhook);

        return limitations
            .Select(limitation => new SetupStep(SetupStepCodes.KnownLimitation, SetupStepStatus.ActionRequired, null,
                new Dictionary<string, string> { [MessagingSetupConstants.FactLimitation] = limitation }))
            .ToList();
    }
}
