// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Checks the provider credentials with the vendor: never for Microsoft Teams (the only test would post
/// a message), with a reason via ICredentialDiagnoser where the adapter offers it, otherwise via the
/// adapter's boolean ValidateConfigAsync. Records the outcome on the context for dependent steps. A vendor
/// call that ended because the token was cancelled is surfaced as cancellation, never as a rejection.
/// </summary>
/// <param name="context">Per-provider diagnosis state; receives CredentialsValid and CredentialFacts</param>
/// <param name="requiredFieldsComplete">Whether the RequiredFields step passed; credentials are only tested then</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class CredentialsStepChecker
{
    public async Task<SetupStep> CheckAsync(ProviderDiagnosisContext context, bool requiredFieldsComplete, CancellationToken ct)
    {
        if (!requiredFieldsComplete)
            return new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.NotChecked, SetupStepDetails.SkippedUntilRequiredFields);

        if (SetupProviderCatalog.Is(context.Provider.ProviderType, MessagingConstants.ProviderTeams))
            return new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.NotChecked, SetupStepDetails.TeamsNotTested);

        if (context.Adapter is ICredentialDiagnoser diagnoser)
            return await DiagnoseAsync(context, diagnoser, ct);

        context.CredentialsValid = await context.Adapter.ValidateConfigAsync(context.Provider.ConfigJson, ct);
        ct.ThrowIfCancellationRequested();
        return context.CredentialsValid
            ? new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.Ok)
            : new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.Error, SetupStepDetails.CredentialsRejected);
    }

    private static async Task<SetupStep> DiagnoseAsync(ProviderDiagnosisContext context, ICredentialDiagnoser diagnoser, CancellationToken ct)
    {
        var diagnosis = await diagnoser.DiagnoseCredentialsAsync(context.Provider.ConfigJson, ct);
        ct.ThrowIfCancellationRequested();
        var facts = CleanFacts(context, diagnosis.Facts);
        context.CredentialsValid = diagnosis.IsValid;
        context.CredentialFacts = diagnosis.Facts;

        if (diagnosis.IsValid)
            return new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.Ok, null, facts);

        var vendorMessage = context.Redactor.Clean(diagnosis.VendorMessage);
        var detail = vendorMessage == null
            ? diagnosis.ReasonCode
            : VendorText.Truncate(string.Format(SetupStepDetails.CredentialErrorFormat, diagnosis.ReasonCode, vendorMessage));

        return new SetupStep(SetupStepCodes.Credentials, SetupStepStatus.Error, detail, facts);
    }

    private static IReadOnlyDictionary<string, string>? CleanFacts(ProviderDiagnosisContext context, IReadOnlyDictionary<string, string>? facts)
    {
        if (facts == null || facts.Count == 0)
            return null;

        return facts.ToDictionary(fact => fact.Key, fact => context.Redactor.Clean(fact.Value) ?? string.Empty);
    }
}
