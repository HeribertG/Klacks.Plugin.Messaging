// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Checks the webhook URL (HTTPS and publicly reachable) and whether the vendor actually delivers to it:
/// Telegram via getWebhookInfo, Viber via the URL returned by the credential check, and Slack/WhatsApp/LINE
/// via observed webhook activity. A delivery counts as verified by an in-memory webhook hit or by a stored
/// inbound message, whichever is newer. Read-only - never re-registers a webhook.
/// </summary>
/// <param name="context">Per-provider diagnosis state; receives WebhookUrlStatus and supplies credential outcome and activity</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class WebhookStepChecker
{
    private const char UrlPathSeparator = '/';

    public SetupStep CheckUrl(ProviderDiagnosisContext context)
    {
        var verdict = WebhookUrlClassifier.Classify(context.ConfiguredWebhookUrl);
        var facts = WebhookUrlFacts(context.ConfiguredWebhookUrl, null);

        if (verdict == WebhookUrlVerdict.Public)
        {
            context.WebhookUrlStatus = SetupStepStatus.Ok;
            return new SetupStep(SetupStepCodes.WebhookUrl, SetupStepStatus.Ok, null, facts);
        }

        context.WebhookUrlStatus = SetupStepStatus.Error;
        return new SetupStep(SetupStepCodes.WebhookUrl, SetupStepStatus.Error, verdict.ToString(), facts);
    }

    public async Task<SetupStep> CheckRegistrationAsync(ProviderDiagnosisContext context, CancellationToken ct)
    {
        if (context.WebhookUrlStatus != SetupStepStatus.Ok)
            return NotChecked(SetupStepDetails.SkippedUntilWebhookUrlUsable);

        var providerType = context.Provider.ProviderType;
        if (SetupProviderCatalog.Is(providerType, MessagingConstants.ProviderTelegram))
            return await CheckTelegramAsync(context, ct);

        if (SetupProviderCatalog.Is(providerType, MessagingConstants.ProviderViber))
            return CheckViber(context, ct);

        return CheckByObservedActivity(context);
    }

    private static async Task<SetupStep> CheckTelegramAsync(ProviderDiagnosisContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!context.CredentialsValid)
            return NotChecked(SetupStepDetails.SkippedUntilCredentialsValid);

        if (context.Adapter is not ITelegramWebhookInspector inspector)
            return NotChecked(SetupStepDetails.WebhookInfoUnavailable);

        var info = await inspector.GetWebhookInfoAsync(context.Provider.ConfigJson, ct);
        ct.ThrowIfCancellationRequested();
        if (info == null)
            return NotChecked(SetupStepDetails.WebhookInfoUnavailable);

        if (!SameUrl(info.Url, context.ConfiguredWebhookUrl))
        {
            return new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.Error, SetupStepDetails.WebhookUrlMismatch,
                WebhookUrlFacts(context.ConfiguredWebhookUrl, context.Redactor.Clean(info.Url)));
        }

        var pendingFacts = new Dictionary<string, string>
        {
            [MessagingSetupConstants.FactPendingUpdates] = info.PendingUpdateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return IsUnresolvedDeliveryError(info, context.LastVerifiedDeliveryUtc)
            ? new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.Error, context.Redactor.Clean(info.LastErrorMessage), pendingFacts)
            : new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.Ok, null, pendingFacts);
    }

    private static bool IsUnresolvedDeliveryError(TelegramWebhookInfo info, DateTime? lastVerifiedDeliveryUtc)
    {
        if (string.IsNullOrWhiteSpace(info.LastErrorMessage))
            return false;

        return lastVerifiedDeliveryUtc == null || info.LastErrorAtUtc == null || info.LastErrorAtUtc > lastVerifiedDeliveryUtc;
    }

    private static SetupStep CheckViber(ProviderDiagnosisContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!context.CredentialsValid)
            return NotChecked(SetupStepDetails.SkippedUntilCredentialsValid);

        string? registeredUrl = null;
        context.CredentialFacts?.TryGetValue(MessagingSetupConstants.FactWebhookUrl, out registeredUrl);

        return SameUrl(registeredUrl, context.ConfiguredWebhookUrl)
            ? new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.Ok)
            : new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.Error, SetupStepDetails.WebhookUrlMismatch,
                WebhookUrlFacts(context.ConfiguredWebhookUrl, context.Redactor.Clean(registeredUrl)));
    }

    private static SetupStep CheckByObservedActivity(ProviderDiagnosisContext context)
    {
        var deliveryObserved = context.LastVerifiedDeliveryUtc != null;

        return deliveryObserved
            ? new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.Ok)
            : new SetupStep(SetupStepCodes.WebhookRegistered, SetupStepStatus.ActionRequired,
                MessagingSetupConstants.ManualConsoleRegistration, WebhookUrlFacts(context.ConfiguredWebhookUrl, null));
    }

    private static bool SameUrl(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(left.Trim().TrimEnd(UrlPathSeparator), right.Trim().TrimEnd(UrlPathSeparator), StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string>? WebhookUrlFacts(string? configuredUrl, string? registeredUrl)
    {
        var facts = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(configuredUrl))
            facts[MessagingSetupConstants.FactWebhookUrl] = configuredUrl;

        if (!string.IsNullOrWhiteSpace(registeredUrl))
            facts[MessagingSetupConstants.FactRegisteredWebhookUrl] = registeredUrl;

        return facts.Count == 0 ? null : facts;
    }

    private static SetupStep NotChecked(string detail) =>
        new(SetupStepCodes.WebhookRegistered, SetupStepStatus.NotChecked, detail);
}
