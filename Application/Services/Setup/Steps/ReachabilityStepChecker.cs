// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Informational step: how many clients hold a messenger contact of the provider's type, next to the
/// number of employees. Never an error; NotChecked when either number is unavailable.
/// </summary>
/// <param name="contactRepository">Counts the messenger contacts of a type</param>
using System.Globalization;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class ReachabilityStepChecker
{
    private readonly IMessengerContactRepository _contactRepository;

    public ReachabilityStepChecker(IMessengerContactRepository contactRepository)
    {
        _contactRepository = contactRepository;
    }

    public async Task<SetupStep> CheckAsync(ProviderDiagnosisContext context, CancellationToken ct)
    {
        if (context.MessengerType == null)
            return new SetupStep(SetupStepCodes.EmployeesReachable, SetupStepStatus.NotChecked);

        if (context.TotalEmployees == null)
            return new SetupStep(SetupStepCodes.EmployeesReachable, SetupStepStatus.NotChecked, SetupStepDetails.EmployeeCountUnavailable);

        var linkedClients = await _contactRepository.CountByTypeAsync(context.MessengerType.Value, ct);
        var facts = new Dictionary<string, string>
        {
            [MessagingSetupConstants.FactLinkedClients] = linkedClients.ToString(CultureInfo.InvariantCulture),
            [MessagingSetupConstants.FactTotalEmployees] = context.TotalEmployees.Value.ToString(CultureInfo.InvariantCulture),
        };

        return new SetupStep(SetupStepCodes.EmployeesReachable, SetupStepStatus.Ok, null, facts);
    }
}
