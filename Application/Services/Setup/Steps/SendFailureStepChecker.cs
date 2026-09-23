// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Reports the most recent failed outbound message of the provider within the lookback window, with the
/// stored vendor error cleaned of configured values and truncated.
/// </summary>
/// <param name="messageRepository">Source of the latest outbound messages of the provider</param>
/// <param name="utcNow">Clock used for the lookback window</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Services.Setup.Steps;

public sealed class SendFailureStepChecker
{
    private const int FirstPageOffset = 0;

    private readonly IMessageRepository _messageRepository;
    private readonly Func<DateTime> _utcNow;

    public SendFailureStepChecker(IMessageRepository messageRepository, Func<DateTime> utcNow)
    {
        _messageRepository = messageRepository;
        _utcNow = utcNow;
    }

    public async Task<SetupStep> CheckAsync(ProviderDiagnosisContext context)
    {
        var recentOutbound = await _messageRepository.GetMessagesAsync(
            context.Provider.Id, MessageDirection.Outbound, null, null, MessagingSetupConstants.SendFailureScanCount, FirstPageOffset);

        var cutoffUtc = _utcNow().AddDays(-MessagingSetupConstants.SendFailureLookbackDays);
        var latestFailure = recentOutbound
            .Where(message => message.Status == MessageStatus.Failed && message.Timestamp >= cutoffUtc)
            .MaxBy(message => message.Timestamp);

        if (latestFailure == null)
            return new SetupStep(SetupStepCodes.LastSendFailure, SetupStepStatus.Ok);

        var detail = context.Redactor.Clean(latestFailure.ErrorMessage) ?? SetupStepDetails.SendFailedWithoutMessage;
        return new SetupStep(SetupStepCodes.LastSendFailure, SetupStepStatus.Error, detail);
    }
}
