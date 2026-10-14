// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Skill for sending messages via a configured messaging provider, to a Klacks client or to an application user. It runs only
/// as the confirmed replay of its own server preview: the host binds provider, recipient address, content type and a hash of the
/// text to the confirmation (SendMessageBinding) and hands the binding over in the context. Without it the skill refuses before
/// it touches anything; with it, the skill resolves the recipient again through IMessageRecipientResolver, the same resolver the
/// preview used, and sends only when the facts are still the confirmed ones. Recipient rules (owner alias, application user, raw
/// address, group visibility of non-admins) live in the resolver.
/// </summary>
/// <param name="provider">The messaging provider name or type (e.g., 'telegram', 'whatsapp'). Required
/// for recipientType 'client'; optional for 'user', where the user's preferred paired channel is used
/// if omitted.</param>
/// <param name="recipient">For recipientType 'client': 'mir' / 'me' / 'myself', a Client name, or a
/// phone number / chat ID. For recipientType 'user': a user name.</param>
/// <param name="content">Message text content</param>
/// <param name="contentType">Content type: text, image, document (default: text)</param>
/// <param name="recipientType">'client' (default) or 'user'</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Application.Services;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation(MessagingConstants.SkillSendMessage)]
public class SendMessageSkill : BaseSkillImplementation
{
    private readonly IMessagingService _messagingService;
    private readonly IMessageRecipientResolver _recipientResolver;

    public SendMessageSkill(IMessagingService messagingService, IMessageRecipientResolver recipientResolver)
    {
        _messagingService = messagingService;
        _recipientResolver = recipientResolver;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        if (context.ConfirmedBinding is not { Kind: MessagingConstants.MessageBindingKind } confirmed)
        {
            return SkillResult.Error(string.Format(PluginConfirmationTexts.BindingRequired, MessagingConstants.SkillSendMessage));
        }

        var request = SendMessageParameters.Parse(parameters);
        var isAdmin = context.UserPermissions.Contains(MessagingConstants.RoleAdmin);

        var resolution = await _recipientResolver.ResolveAsync(request, isAdmin, cancellationToken);
        if (!resolution.Success)
        {
            return SkillResult.Error(resolution.Error!);
        }

        var current = SendMessageBinding.ItemsFor(resolution, request.Content, request.ContentType);
        if (!SendMessageBinding.Matches(confirmed, current))
        {
            return SkillResult.Error(PluginConfirmationTexts.ScopeChanged);
        }

        var message = new SendMessageRequest(
            resolution.Identifier!, request.Content, request.ContentType, SenderDisplayName: MessagingConstants.KlacksySenderDisplayName);
        var result = await _messagingService.SendMessageAsync(resolution.ProviderName!, message, cancellationToken);

        if (!result.Success)
        {
            return SkillResult.Error($"Failed to send message via {resolution.ProviderName}: {result.ErrorMessage}");
        }

        return SkillResult.SuccessResult(
            new
            {
                Provider = resolution.ProviderName,
                Recipient = resolution.DisplayName,
                Identifier = resolution.Identifier,
                MessageId = result.ExternalMessageId,
                Status = "sent"
            },
            $"Message sent successfully via {resolution.ProviderName} to {resolution.DisplayName} ({resolution.Identifier}).");
    }
}
