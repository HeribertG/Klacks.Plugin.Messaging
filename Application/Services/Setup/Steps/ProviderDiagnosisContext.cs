// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Per-provider working state shared by the setup step checkers: the provider, its adapter, the inbound
/// activity snapshot, the newest stored inbound message, and the outcomes of earlier steps that later
/// steps depend on. LastVerifiedDeliveryUtc combines the in-memory webhook hit with the newest stored
/// inbound message, so a restart that empties the tracker does not hide a delivery that demonstrably worked.
/// </summary>
/// <param name="provider">The provider being diagnosed</param>
/// <param name="adapter">Adapter resolved for the provider type; optional capabilities are queried on it</param>
/// <param name="activity">In-memory inbound activity observed for the provider</param>
/// <param name="latestInboundAtUtc">Timestamp of the newest stored inbound message, or null when none is stored</param>
/// <param name="enabledProviderCount">Number of enabled providers across the installation</param>
/// <param name="totalEmployees">Number of employees, or null when it could not be determined</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class ProviderDiagnosisContext
{
    public ProviderDiagnosisContext(
        MessagingProvider provider,
        IMessagingProviderAdapter adapter,
        InboundActivitySnapshot activity,
        DateTime? latestInboundAtUtc,
        int enabledProviderCount,
        int? totalEmployees)
    {
        Provider = provider;
        Adapter = adapter;
        Activity = activity;
        LatestInboundAtUtc = latestInboundAtUtc;
        EnabledProviderCount = enabledProviderCount;
        TotalEmployees = totalEmployees;
        Redactor = new SetupSecretRedactor(provider.ProviderType, provider.ConfigJson, provider.WebhookSecret);
        ConfiguredWebhookUrl = SetupConfigReader.GetString(provider.ConfigJson, SetupConfigKeys.WebhookUrl);
        IsInboundCapable = SetupProviderCatalog.IsInboundCapable(provider.ProviderType);
        NeedsWebhook = SetupProviderCatalog.NeedsWebhook(provider.ProviderType, provider.ConfigJson);
        IsSlackWebhookMode = SetupProviderCatalog.IsSlackWebhookMode(provider.ProviderType, provider.ConfigJson);
        MessengerType = Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var type) ? type : null;
    }

    public MessagingProvider Provider { get; }

    public IMessagingProviderAdapter Adapter { get; }

    public InboundActivitySnapshot Activity { get; }

    public DateTime? LatestInboundAtUtc { get; }

    public DateTime? LastVerifiedDeliveryUtc =>
        LatestInboundAtUtc == null || Activity.LastWebhookHitUtc > LatestInboundAtUtc
            ? Activity.LastWebhookHitUtc
            : LatestInboundAtUtc;

    public int EnabledProviderCount { get; }

    public int? TotalEmployees { get; }

    public SetupSecretRedactor Redactor { get; }

    public string? ConfiguredWebhookUrl { get; }

    public bool IsInboundCapable { get; }

    public bool NeedsWebhook { get; }

    public bool IsSlackWebhookMode { get; }

    public MessengerType? MessengerType { get; }

    public bool CredentialsValid { get; set; }

    public IReadOnlyDictionary<string, string>? CredentialFacts { get; set; }

    public SetupStepStatus? WebhookUrlStatus { get; set; }
}
