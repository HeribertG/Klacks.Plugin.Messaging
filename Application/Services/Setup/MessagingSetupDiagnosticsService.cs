// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Composes the messenger setup report: a plugin-level ProviderPresent step and, per provider, the steps
/// of the commissioning catalog in order (steps that do not apply to the type are left out) plus the
/// first open step as NextStep. A failure while diagnosing one provider is isolated to that provider, and
/// the vendor calls of each provider share one time budget: when it runs out, the vendor step that was
/// waiting reports Unreachable and the remaining providers are still diagnosed. Caller cancellation
/// always propagates. Read-only: never re-registers webhooks and never tests Microsoft Teams.
/// </summary>
/// <param name="providerRepository">Source of all configured providers</param>
/// <param name="adapterFactory">Resolves the adapter whose optional diagnosis capabilities are used</param>
/// <param name="activityTracker">In-memory inbound activity per provider</param>
/// <param name="messageRepository">Stored inbound and outbound messages</param>
/// <param name="contactRepository">Messenger contacts, counted per type</param>
/// <param name="ownerReader">Owner messenger identities</param>
/// <param name="employeeReader">Employee clients, counted once per report</param>
/// <param name="utcNow">Clock for the send failure lookback window</param>
/// <param name="vendorTimeout">Time budget for the vendor calls of one provider; defaults to ProviderVendorTimeoutSeconds</param>
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Services.Setup.Steps;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public class MessagingSetupDiagnosticsService : IMessagingSetupDiagnosticsService
{
    private static readonly HashSet<string> InformationalStepCodes = new(StringComparer.Ordinal)
    {
        SetupStepCodes.EmployeesReachable,
        SetupStepCodes.KnownLimitation,
    };

    private const int LatestInboundMessageCount = 1;
    private const int FirstPageOffset = 0;

    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessagingProviderAdapterFactory _adapterFactory;
    private readonly IMessagingInboundActivityTracker _activityTracker;
    private readonly IMessageRepository _messageRepository;
    private readonly IEmployeeClientReader _employeeReader;
    private readonly TimeSpan _vendorTimeout;
    private readonly ILogger<MessagingSetupDiagnosticsService> _logger;
    private readonly CredentialsStepChecker _credentialsChecker = new();
    private readonly WebhookStepChecker _webhookChecker = new();
    private readonly KnownLimitationStepChecker _limitationChecker = new();
    private readonly InboundStepChecker _inboundChecker;
    private readonly SendFailureStepChecker _sendFailureChecker;
    private readonly ReachabilityStepChecker _reachabilityChecker;

    public MessagingSetupDiagnosticsService(
        IMessagingProviderRepository providerRepository,
        IMessagingProviderAdapterFactory adapterFactory,
        IMessagingInboundActivityTracker activityTracker,
        IMessageRepository messageRepository,
        IMessengerContactRepository contactRepository,
        IOwnerMessengerReader ownerReader,
        IEmployeeClientReader employeeReader,
        ILogger<MessagingSetupDiagnosticsService> logger)
        : this(providerRepository, adapterFactory, activityTracker, messageRepository, contactRepository,
            ownerReader, employeeReader, logger, () => DateTime.UtcNow)
    {
    }

    public MessagingSetupDiagnosticsService(
        IMessagingProviderRepository providerRepository,
        IMessagingProviderAdapterFactory adapterFactory,
        IMessagingInboundActivityTracker activityTracker,
        IMessageRepository messageRepository,
        IMessengerContactRepository contactRepository,
        IOwnerMessengerReader ownerReader,
        IEmployeeClientReader employeeReader,
        ILogger<MessagingSetupDiagnosticsService> logger,
        Func<DateTime> utcNow,
        TimeSpan? vendorTimeout = null)
    {
        _providerRepository = providerRepository;
        _adapterFactory = adapterFactory;
        _activityTracker = activityTracker;
        _messageRepository = messageRepository;
        _employeeReader = employeeReader;
        _logger = logger;
        _vendorTimeout = vendorTimeout ?? TimeSpan.FromSeconds(MessagingSetupConstants.ProviderVendorTimeoutSeconds);
        _inboundChecker = new InboundStepChecker(ownerReader);
        _sendFailureChecker = new SendFailureStepChecker(messageRepository, utcNow);
        _reachabilityChecker = new ReachabilityStepChecker(contactRepository);
    }

    public async Task<MessagingSetupReport> DiagnoseAsync(CancellationToken ct = default)
    {
        var providers = await _providerRepository.GetAllAsync();
        var pluginSteps = new List<SetupStep> { BuildProviderPresentStep(providers) };
        if (providers.Count == 0)
            return new MessagingSetupReport(pluginSteps, []);

        var enabledProviderCount = providers.Count(provider => provider.IsEnabled);
        var totalEmployees = providers.Any(provider => SetupProviderCatalog.IsInboundCapable(provider.ProviderType))
            ? await TryCountEmployeesAsync(ct)
            : null;

        var reports = new List<ProviderSetupReport>(providers.Count);
        foreach (var provider in providers)
            reports.Add(await DiagnoseProviderSafelyAsync(provider, enabledProviderCount, totalEmployees, ct));

        return new MessagingSetupReport(pluginSteps, reports);
    }

    private static SetupStep BuildProviderPresentStep(IReadOnlyList<MessagingProvider> providers)
    {
        if (providers.Count == 0)
            return new SetupStep(SetupStepCodes.ProviderPresent, SetupStepStatus.ActionRequired, SetupStepDetails.NoProviderConfigured);

        return providers.Any(provider => provider.IsEnabled)
            ? new SetupStep(SetupStepCodes.ProviderPresent, SetupStepStatus.Ok)
            : new SetupStep(SetupStepCodes.ProviderPresent, SetupStepStatus.ActionRequired, SetupStepDetails.NoProviderEnabled);
    }

    private async Task<int?> TryCountEmployeesAsync(CancellationToken ct)
    {
        try
        {
            var employees = await _employeeReader.GetAllEmployeesAsync(ct);
            return employees.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Messaging setup diagnosis could not count employees");
            return null;
        }
    }

    private async Task<ProviderSetupReport> DiagnoseProviderSafelyAsync(
        MessagingProvider provider, int enabledProviderCount, int? totalEmployees, CancellationToken ct)
    {
        try
        {
            return await DiagnoseProviderAsync(provider, enabledProviderCount, totalEmployees, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Messaging setup diagnosis failed for provider {ProviderName}", provider.Name);
            var failedStep = new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.Error, CredentialReasonCodes.UnexpectedResponse);
            return new ProviderSetupReport(provider.Name, provider.ProviderType, provider.IsEnabled, [failedStep], failedStep);
        }
    }

    private async Task<ProviderSetupReport> DiagnoseProviderAsync(
        MessagingProvider provider, int enabledProviderCount, int? totalEmployees, CancellationToken ct)
    {
        var adapter = _adapterFactory.Create(provider.ProviderType);
        var latestInboundAtUtc = await LoadLatestInboundAtUtcAsync(provider);
        var context = new ProviderDiagnosisContext(
            provider, adapter, _activityTracker.GetSnapshot(provider.Id), latestInboundAtUtc, enabledProviderCount, totalEmployees);

        using var vendorBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        vendorBudget.CancelAfter(_vendorTimeout);

        var requiredFieldsStep = BuildRequiredFieldsStep(context);
        var steps = new List<SetupStep>
        {
            new(SetupStepCodes.ProviderEnabled, provider.IsEnabled ? SetupStepStatus.Ok : SetupStepStatus.ActionRequired),
            requiredFieldsStep,
            await RunVendorStepAsync(SetupStepCodes.Credentials,
                () => _credentialsChecker.CheckAsync(context, requiredFieldsStep.Status == SetupStepStatus.Ok, vendorBudget.Token), ct),
        };

        if (context.NeedsWebhook)
        {
            steps.Add(_webhookChecker.CheckUrl(context));
            steps.Add(await RunVendorStepAsync(SetupStepCodes.WebhookRegistered,
                () => _webhookChecker.CheckRegistrationAsync(context, vendorBudget.Token), ct));
        }

        if (context.IsInboundCapable)
        {
            steps.Add(_inboundChecker.CheckInbound(context));
            steps.Add(await _inboundChecker.CheckOwnerAsync(context, ct));
        }

        steps.Add(await _sendFailureChecker.CheckAsync(context));

        if (context.IsInboundCapable)
            steps.Add(await _reachabilityChecker.CheckAsync(context, ct));

        steps.AddRange(_limitationChecker.Check(context));

        return new ProviderSetupReport(provider.Name, provider.ProviderType, provider.IsEnabled, steps, FindNextStep(steps));
    }

    private async Task<DateTime?> LoadLatestInboundAtUtcAsync(MessagingProvider provider)
    {
        var latestInbound = await _messageRepository.GetMessagesAsync(
            provider.Id, MessageDirection.Inbound, null, null, LatestInboundMessageCount, FirstPageOffset);

        return latestInbound.Count == 0 ? null : latestInbound.Max(message => message.Timestamp);
    }

    private static async Task<SetupStep> RunVendorStepAsync(string stepCode, Func<Task<SetupStep>> check, CancellationToken callerToken)
    {
        try
        {
            return await check();
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            return new SetupStep(stepCode, SetupStepStatus.Error, CredentialReasonCodes.Unreachable);
        }
    }

    private static SetupStep BuildRequiredFieldsStep(ProviderDiagnosisContext context)
    {
        var missing = ProviderRequiredConfigFields.GetMissing(context.Provider.ProviderType, context.Provider.ConfigJson).ToList();

        if (context.IsSlackWebhookMode && SetupConfigReader.GetString(context.Provider.ConfigJson, SetupConfigKeys.SigningSecret) == null)
            missing.Add(SetupConfigKeys.SigningSecret);

        if (missing.Count == 0)
            return new SetupStep(SetupStepCodes.RequiredFields, SetupStepStatus.Ok);

        var facts = new Dictionary<string, string>
        {
            [MessagingSetupConstants.FactMissingFields] = string.Join(SetupStepDetails.ListSeparator, missing)
        };
        return new SetupStep(SetupStepCodes.RequiredFields, SetupStepStatus.ActionRequired, null, facts);
    }

    private static SetupStep? FindNextStep(IEnumerable<SetupStep> steps)
    {
        return steps.FirstOrDefault(step =>
            (step.Status == SetupStepStatus.Error || step.Status == SetupStepStatus.ActionRequired)
            && !InformationalStepCodes.Contains(step.Code));
    }
}
