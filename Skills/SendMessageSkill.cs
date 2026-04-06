// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Skill for sending messages via a configured messaging provider.
/// Resolves the recipient as one of: a 'mir' / 'me' alias for the Klacks owner
/// (looked up in the APP_OWNER_MESSENGERS jsonb setting), a contact name (looked
/// up in messenger_contact via Client name search), or a literal phone number /
/// chat ID that is passed through unchanged.
/// </summary>
/// <param name="provider">The messaging provider name or type (e.g., 'telegram', 'whatsapp')</param>
/// <param name="recipient">'mir' / 'me' / 'myself', a Client name, or a phone number / chat ID</param>
/// <param name="content">Message text content</param>
/// <param name="contentType">Content type: text, image, document (default: text)</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("send_message")]
public class SendMessageSkill : BaseSkillImplementation
{
    private static readonly HashSet<string> SelfAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "mir", "ich", "me", "myself", "self"
    };

    private static readonly Dictionary<string, MessengerType> ProviderToMessengerType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["telegram"] = MessengerType.Telegram,
        ["whatsapp"] = MessengerType.WhatsApp,
        ["signal"] = MessengerType.Signal,
        ["sms"] = MessengerType.Sms,
        ["threema"] = MessengerType.Threema,
        ["viber"] = MessengerType.Viber,
        ["line"] = MessengerType.Line,
        ["kakaotalk"] = MessengerType.KakaoTalk,
        ["wechat"] = MessengerType.WeChat,
        ["zalo"] = MessengerType.Zalo,
        ["microsoftteams"] = MessengerType.MicrosoftTeams,
        ["teams"] = MessengerType.MicrosoftTeams,
        ["slack"] = MessengerType.Slack
    };

    private readonly IMessagingService _messagingService;
    private readonly IMessengerContactRepository _messengerContactRepository;
    private readonly IOwnerMessengerReader _ownerMessengerReader;

    public SendMessageSkill(
        IMessagingService messagingService,
        IMessengerContactRepository messengerContactRepository,
        IOwnerMessengerReader ownerMessengerReader)
    {
        _messagingService = messagingService;
        _messengerContactRepository = messengerContactRepository;
        _ownerMessengerReader = ownerMessengerReader;
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

        var resolved = await ResolveRecipientAsync(recipient, provider, cancellationToken);
        if (resolved == null)
        {
            var trimmed = recipient.Trim();
            if (SelfAliases.Contains(trimmed))
            {
                return SkillResult.Error($"No '{provider}' identifier is configured for the owner. Open Settings -> Owner-Messenger and add a {provider} entry.");
            }

            if (ProviderToMessengerType.TryGetValue(provider, out var messengerType))
            {
                return SkillResult.Error($"No {messengerType} contact found for '{recipient}'. The client must have a {messengerType} entry in their Messenger tab.");
            }

            return SkillResult.Error($"Unknown messaging provider '{provider}'.");
        }

        var request = new SendMessageRequest(resolved.Identifier, content, contentType);
        var result = await _messagingService.SendMessageAsync(provider, request, cancellationToken);

        if (!result.Success)
        {
            return SkillResult.Error($"Failed to send message via {provider}: {result.ErrorMessage}");
        }

        return SkillResult.SuccessResult(
            new
            {
                Provider = provider,
                Recipient = resolved.DisplayName,
                Identifier = resolved.Identifier,
                MessageId = result.ExternalMessageId,
                Status = "sent"
            },
            $"Message sent successfully via {provider} to {resolved.DisplayName} ({resolved.Identifier}).");
    }

    private async Task<ResolvedRecipient?> ResolveRecipientAsync(string recipient, string provider, CancellationToken ct)
    {
        var trimmed = recipient.Trim();

        if (!ProviderToMessengerType.TryGetValue(provider, out var messengerType))
        {
            return IsPhoneNumber(trimmed) ? new ResolvedRecipient(trimmed, trimmed) : null;
        }

        if (SelfAliases.Contains(trimmed))
        {
            return await ResolveSelfAsync(messengerType, ct);
        }

        if (IsPhoneNumber(trimmed))
        {
            return new ResolvedRecipient(trimmed, trimmed);
        }

        return await ResolveByClientNameAsync(trimmed, messengerType, ct);
    }

    private async Task<ResolvedRecipient?> ResolveSelfAsync(MessengerType messengerType, CancellationToken ct)
    {
        var ownerEntry = await _ownerMessengerReader.GetByTypeAsync(messengerType, ct);
        if (ownerEntry == null)
            return null;

        var ownerName = await _ownerMessengerReader.GetOwnerDisplayNameAsync(ct);
        var displayName = string.IsNullOrWhiteSpace(ownerName) ? "Self" : ownerName!;
        return new ResolvedRecipient(displayName, ownerEntry.Value.Trim());
    }

    private async Task<ResolvedRecipient?> ResolveByClientNameAsync(string nameQuery, MessengerType messengerType, CancellationToken ct)
    {
        var matches = await _messengerContactRepository.SearchByClientNameAsync(nameQuery, messengerType, ct);
        var contact = matches.FirstOrDefault();
        if (contact == null)
            return null;

        return new ResolvedRecipient(nameQuery, contact.Value.Trim());
    }

    private static bool IsPhoneNumber(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return false;
        if (trimmed.StartsWith('+')) return true;
        return trimmed.All(c => char.IsDigit(c) || c == '-' || c == ' ');
    }

    private record ResolvedRecipient(string DisplayName, string Identifier);
}
