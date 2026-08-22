// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Skill for sending messages via a configured messaging provider, to a Klacks client or to an
/// application user. For recipientType 'client' (default), resolves the recipient as one of: a
/// 'mir' / 'me' alias for the Klacks owner (looked up in the APP_OWNER_MESSENGERS jsonb setting), a
/// contact name (looked up in messenger_contact via Client name search), or a literal phone number /
/// chat ID that is passed through unchanged. For recipientType 'user', resolves the recipient by
/// AppUser name search and sends through that user's paired messenger channel.
/// </summary>
/// <param name="provider">The messaging provider name or type (e.g., 'telegram', 'whatsapp'). Required
/// for recipientType 'client'; optional for 'user', where the user's preferred paired channel is used
/// if omitted.</param>
/// <param name="recipient">For recipientType 'client': 'mir' / 'me' / 'myself', a Client name, or a
/// phone number / chat ID. For recipientType 'user': a user name.</param>
/// <param name="content">Message text content</param>
/// <param name="contentType">Content type: text, image, document (default: text)</param>
/// <param name="recipientType">'client' (default) or 'user'</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("send_message")]
public class SendMessageSkill : BaseSkillImplementation
{
    private const string RecipientTypeClient = "client";
    private const string RecipientTypeUser = "user";

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
    private readonly IAppUserDirectoryReader _appUserDirectoryReader;
    private readonly IUserMessengerContactRepository _userMessengerContactRepository;
    private readonly IMessagingProviderRepository _providerRepository;

    public SendMessageSkill(
        IMessagingService messagingService,
        IMessengerContactRepository messengerContactRepository,
        IOwnerMessengerReader ownerMessengerReader,
        IAppUserDirectoryReader appUserDirectoryReader,
        IUserMessengerContactRepository userMessengerContactRepository,
        IMessagingProviderRepository providerRepository)
    {
        _messagingService = messagingService;
        _messengerContactRepository = messengerContactRepository;
        _ownerMessengerReader = ownerMessengerReader;
        _appUserDirectoryReader = appUserDirectoryReader;
        _userMessengerContactRepository = userMessengerContactRepository;
        _providerRepository = providerRepository;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var recipientType = GetParameter(parameters, "recipientType", RecipientTypeClient)!.Trim().ToLowerInvariant();
        var recipient = GetRequiredString(parameters, "recipient");
        var content = GetRequiredString(parameters, "content");
        var contentType = GetParameter(parameters, "contentType", "text")!;
        var provider = GetParameter<string>(parameters, "provider");

        if (recipientType == RecipientTypeUser)
        {
            return await ExecuteUserSendAsync(recipient, provider, content, contentType, cancellationToken);
        }

        if (recipientType != RecipientTypeClient)
        {
            return SkillResult.Error($"Unknown recipientType '{recipientType}'. Use 'client' or 'user'.");
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            var resolved = await ResolveSoleEnabledProviderAsync(cancellationToken);
            if (resolved.Error != null)
            {
                return SkillResult.Error(resolved.Error);
            }

            provider = resolved.ProviderType!;
        }

        var lookup = await ResolveClientRecipientAsync(recipient, provider, cancellationToken);
        if (lookup.Recipient == null)
        {
            if (lookup.AmbiguousNames.Count > 1)
            {
                return SkillResult.Error(
                    $"Multiple clients match '{recipient}': {string.Join(", ", lookup.AmbiguousNames)}. Please specify which one.");
            }

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

        var request = new SendMessageRequest(lookup.Recipient.Identifier, content, contentType, SenderDisplayName: MessagingConstants.KlacksySenderDisplayName);
        var result = await _messagingService.SendMessageAsync(provider, request, cancellationToken);

        if (!result.Success)
        {
            return SkillResult.Error($"Failed to send message via {provider}: {result.ErrorMessage}");
        }

        return SkillResult.SuccessResult(
            new
            {
                Provider = provider,
                Recipient = lookup.Recipient.DisplayName,
                Identifier = lookup.Recipient.Identifier,
                MessageId = result.ExternalMessageId,
                Status = "sent"
            },
            $"Message sent successfully via {provider} to {lookup.Recipient.DisplayName} ({lookup.Recipient.Identifier}).");
    }

    /// <summary>
    /// Auto-resolves the provider when the caller omitted it: with exactly one enabled provider there
    /// is nothing to ask about, so the skill must not force a channel choice the installation cannot
    /// even offer. Returns the provider's type (e.g. "Slack"), not its admin-assigned Name, matching
    /// what ProviderToMessengerType and the downstream provider lookup both expect.
    /// </summary>
    private async Task<(string? ProviderType, string? Error)> ResolveSoleEnabledProviderAsync(CancellationToken ct)
    {
        var enabledProviders = await _providerRepository.GetEnabledAsync();

        if (enabledProviders.Count == 0)
        {
            return (null, "No messaging provider is configured and enabled. An admin must set one up under Settings -> Messaging.");
        }

        if (enabledProviders.Count > 1)
        {
            var names = string.Join(", ", enabledProviders.Select(p => p.Name));
            return (null, $"The 'provider' parameter is required: multiple messaging providers are enabled ({names}). Specify which one to use.");
        }

        return (enabledProviders[0].ProviderType, null);
    }

    private async Task<RecipientLookup> ResolveClientRecipientAsync(string recipient, string provider, CancellationToken ct)
    {
        var trimmed = recipient.Trim();

        if (!ProviderToMessengerType.TryGetValue(provider, out var messengerType))
        {
            return IsPhoneNumber(trimmed)
                ? new RecipientLookup(new ResolvedRecipient(trimmed, trimmed), Array.Empty<string>())
                : new RecipientLookup(null, Array.Empty<string>());
        }

        if (SelfAliases.Contains(trimmed))
        {
            var self = await ResolveSelfAsync(messengerType, ct);
            return new RecipientLookup(self, Array.Empty<string>());
        }

        if (IsPhoneNumber(trimmed))
        {
            return new RecipientLookup(new ResolvedRecipient(trimmed, trimmed), Array.Empty<string>());
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

    /// <summary>
    /// Lone-match rule (recipe-authoring.md §4): a name query must resolve to exactly one client or
    /// the skill has to report the real candidates instead of guessing. A prior version silently
    /// picked the first SQL row on multiple matches, which could message the wrong client.
    /// </summary>
    private async Task<RecipientLookup> ResolveByClientNameAsync(string nameQuery, MessengerType messengerType, CancellationToken ct)
    {
        var matches = await _messengerContactRepository.SearchByClientNameAsync(nameQuery, messengerType, ct);

        if (matches.Count == 0)
            return new RecipientLookup(null, Array.Empty<string>());

        if (matches.Count > 1)
            return new RecipientLookup(null, matches.Select(BuildClientDisplayName).ToList());

        var match = matches[0];
        return new RecipientLookup(new ResolvedRecipient(BuildClientDisplayName(match), match.Value.Trim()), Array.Empty<string>());
    }

    private async Task<SkillResult> ExecuteUserSendAsync(
        string recipient,
        string? provider,
        string content,
        string contentType,
        CancellationToken ct)
    {
        var candidates = await _appUserDirectoryReader.SearchByNameAsync(recipient.Trim(), ct);

        if (candidates.Count == 0)
        {
            return SkillResult.Error($"No active user found matching '{recipient}'.");
        }

        if (candidates.Count > 1)
        {
            return SkillResult.Error(
                $"Multiple users match '{recipient}': {string.Join(", ", candidates.Select(BuildUserDisplayName))}. Please specify which one.");
        }

        var user = candidates[0];
        var displayName = BuildUserDisplayName(user);

        UserMessengerContact? channel;
        string providerName;

        if (!string.IsNullOrWhiteSpace(provider))
        {
            if (!ProviderToMessengerType.TryGetValue(provider, out var messengerType))
            {
                return SkillResult.Error($"Unknown messaging provider '{provider}'.");
            }

            channel = await _userMessengerContactRepository.GetByUserAndTypeAsync(user.UserId, messengerType, ct);
            if (channel == null)
            {
                return await BuildNoChannelErrorAsync(user.UserId, displayName, ct, requestedProvider: provider);
            }

            providerName = provider;
        }
        else
        {
            channel = await _userMessengerContactRepository.GetPreferredAsync(user.UserId, ct);
            if (channel == null)
            {
                return await BuildNoChannelErrorAsync(user.UserId, displayName, ct);
            }

            var enabledProviders = await _providerRepository.GetEnabledAsync();
            var matchingProvider = enabledProviders.FirstOrDefault(p =>
                ProviderToMessengerType.TryGetValue(p.ProviderType, out var type) && type == channel.Type);

            if (matchingProvider == null)
            {
                return SkillResult.Error(
                    $"'{displayName}' has a paired {channel.Type} channel, but no enabled {channel.Type} provider is configured.");
            }

            providerName = matchingProvider.Name;
        }

        var request = new SendMessageRequest(channel.Value, content, contentType, SenderDisplayName: MessagingConstants.KlacksySenderDisplayName);
        var result = await _messagingService.SendMessageAsync(providerName, request, ct);

        if (!result.Success)
        {
            return SkillResult.Error($"Failed to send message via {providerName}: {result.ErrorMessage}");
        }

        return SkillResult.SuccessResult(
            new
            {
                Provider = providerName,
                Recipient = displayName,
                Identifier = channel.Value,
                MessageId = result.ExternalMessageId,
                Status = "sent"
            },
            $"Message sent successfully via {providerName} to {displayName} ({channel.Value}).");
    }

    private async Task<SkillResult> BuildNoChannelErrorAsync(
        string userId,
        string displayName,
        CancellationToken ct,
        string? requestedProvider = null)
    {
        var pairedChannels = await _userMessengerContactRepository.GetByUserIdAsync(userId, ct);
        var pairedTypesText = pairedChannels.Count == 0
            ? "none"
            : string.Join(", ", pairedChannels.Select(c => c.Type.ToString()).Distinct());

        var missing = requestedProvider != null ? $"a paired {requestedProvider} channel" : "any paired messenger channel";
        return SkillResult.Error(
            $"'{displayName}' has no {missing}. Paired channels: {pairedTypesText}. " +
            "An admin can send an invite from the user administration to pair a channel.");
    }

    private static string BuildClientDisplayName(ClientMessengerMatch match)
    {
        var combined = $"{match.Name}, {match.FirstName}".Trim().Trim(',', ' ');
        if (combined.Length > 0)
            return combined;

        return string.IsNullOrWhiteSpace(match.Company) ? match.ClientId.ToString() : match.Company;
    }

    private static string BuildUserDisplayName(AppUserDirectoryInfo user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return name.Length > 0 ? name : user.UserId;
    }

    private static bool IsPhoneNumber(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return false;
        if (trimmed.StartsWith('+')) return true;
        return trimmed.All(c => char.IsDigit(c) || c == '-' || c == ' ');
    }

    private record ResolvedRecipient(string DisplayName, string Identifier);

    private record RecipientLookup(ResolvedRecipient? Recipient, IReadOnlyList<string> AmbiguousNames);
}
