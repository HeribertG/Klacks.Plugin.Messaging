// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Skill for sending messages via a configured messaging provider.
/// Used by the chatbot to send WhatsApp, Telegram, Signal, or SMS messages.
/// </summary>
/// <param name="provider">The messaging provider name (e.g., "telegram", "whatsapp")</param>
/// <param name="recipient">Recipient phone number or user ID</param>
/// <param name="content">Message text content</param>
/// <param name="contentType">Content type: text, image, document (default: text)</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("send_message")]
public class SendMessageSkill : BaseSkillImplementation
{
    private readonly IMessagingService _messagingService;

    public SendMessageSkill(IMessagingService messagingService)
    {
        _messagingService = messagingService;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var provider = GetRequiredString(parameters, "provider");
        var recipient = GetRequiredString(parameters, "recipient");
        var content = GetRequiredString(parameters, "content");
        var contentType = GetParameter<string>(parameters, "contentType", "text")!;

        var request = new SendMessageRequest(recipient, content, contentType);
        var result = await _messagingService.SendMessageAsync(provider, request, cancellationToken);

        if (!result.Success)
        {
            return SkillResult.Error($"Failed to send message via {provider}: {result.ErrorMessage}");
        }

        return SkillResult.SuccessResult(
            new
            {
                Provider = provider,
                Recipient = recipient,
                MessageId = result.ExternalMessageId,
                Status = "sent"
            },
            $"Message sent successfully via {provider} to {recipient}.");
    }
}
