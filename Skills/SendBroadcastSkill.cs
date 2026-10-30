// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Skill for one broadcast to a group or to client id numbers on one messenger channel. It runs only as the confirmed replay of its
/// own server preview: without a binding it refuses before it touches anything; with one it computes the plan again through
/// IBroadcastPlanner, the path the preview used, and sends only when provider, recipients and text are still the confirmed ones.
/// Plan and send run under IBroadcastRunLock, taken without waiting, so a second call cannot pass the repeat check of a run in
/// progress; it is told so and nothing is sent.
/// It then sends exactly the compared addresses through the broadcast path of MessagingService, which iterates on the server and
/// isolates a failure per recipient, so the model never loops. The answer carries counts only, never an address or the text.
/// </summary>
/// <param name="groupId">Group of the audience; its subgroups belong to it</param>
/// <param name="idNumbers">Client id numbers of the audience, instead of a group</param>
/// <param name="content">The text, sent verbatim</param>
/// <param name="provider">Provider name or type; the sole enabled provider when omitted</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Application.Services;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation(MessagingConstants.SkillSendBroadcast)]
public class SendBroadcastSkill : BaseSkillImplementation
{
    private const string NothingDeliveredMessage = "The broadcast via {0} reached nobody: {1} failed, {2} without an address.";
    private const string SummaryMessage =
        "Broadcast via {0}: {1} sent, {2} failed, {3} skipped without an address, {4} left out because they already had this text. " +
        "The run is finished; do not call this skill again for it. Sending the same text again within the repeat window reaches only those who did not get it.";

    private readonly IBroadcastPlanner _planner;
    private readonly IMessagingService _messagingService;
    private readonly IBroadcastRunLock _runLock;

    public SendBroadcastSkill(IBroadcastPlanner planner, IMessagingService messagingService, IBroadcastRunLock runLock)
    {
        _planner = planner;
        _messagingService = messagingService;
        _runLock = runLock;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        if (context.ConfirmedBinding is not { Kind: MessagingConstants.BroadcastBindingKind } confirmed)
        {
            return SkillResult.Error(string.Format(PluginConfirmationTexts.BindingRequired, MessagingConstants.SkillSendBroadcast));
        }

        SendBroadcastParameters request;
        try
        {
            request = SendBroadcastParameters.Parse(parameters);
        }
        catch (ArgumentException ex)
        {
            return SkillResult.Error(ex.Message);
        }

        await using var runHold = await _runLock.TryAcquireAsync(cancellationToken);
        if (runHold == null)
        {
            return SkillResult.Error(MessagingConstants.BroadcastAlreadyRunningError);
        }

        var isAdmin = context.UserPermissions.Contains(MessagingConstants.RoleAdmin);
        var plan = await _planner.PlanAsync(request, isAdmin, cancellationToken);
        if (!plan.Success)
        {
            return SkillResult.Error(plan.Error!);
        }

        var recipients = plan.Recipients!;
        if (!SendBroadcastBinding.Matches(confirmed, SendBroadcastBinding.ItemsFor(recipients, request.Content)))
        {
            return SkillResult.Error(PluginConfirmationTexts.ScopeChanged);
        }

        try
        {
            var result = await _messagingService.SendBroadcastToRecipientsAsync(
                recipients, request.Content, MessagingConstants.DefaultContentType, cancellationToken);

            if (result.Sent == 0)
            {
                return SkillResult.Error(string.Format(NothingDeliveredMessage, recipients.ProviderName, result.Failed, result.SkippedNoContact));
            }

            return SkillResult.SuccessResult(
                new
                {
                    result.BroadcastId,
                    Provider = recipients.ProviderName,
                    result.Sent,
                    result.Failed,
                    Skipped = result.SkippedNoContact,
                    plan.AlreadyReceived
                },
                string.Format(SummaryMessage, recipients.ProviderName, result.Sent, result.Failed, result.SkippedNoContact, plan.AlreadyReceived));
        }
        catch (InvalidOperationException ex)
        {
            return SkillResult.Error(ex.Message);
        }
    }
}
