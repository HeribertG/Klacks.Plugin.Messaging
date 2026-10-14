// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Resolves the recipient of a send_message call. For recipientType 'client' (default) the recipient is one of: a 'mir' / 'me'
/// alias for the Klacks owner (looked up in the APP_OWNER_MESSENGERS jsonb setting), a contact name (looked up in
/// messenger_contact via client name search) or a literal phone number / chat ID that is passed through unchanged; for
/// recipientType 'user' the recipient is found by application-user name search and reached through that user's paired messenger
/// channel. The owner alias, the application-user path and an address without a known contact are administrator-only; every
/// other caller reaches only contacts of clients its group visibility lets it see, exactly like POST api/messaging/messages/send,
/// and a hidden client is answered like a missing contact (also in the candidate list of an ambiguous name).
/// </summary>
/// <param name="messagingService">Attributes an address to a client</param>
/// <param name="messengerContactRepository">Client name search against the messenger contacts</param>
/// <param name="ownerMessengerReader">The owner's own messenger entries</param>
/// <param name="appUserDirectoryReader">Application user name search</param>
/// <param name="userMessengerContactRepository">Paired messenger channels of application users</param>
/// <param name="providerRepository">Enabled providers, for the sole-provider default</param>
/// <param name="accessScope">Group visibility of the calling user</param>

using System.Globalization;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public class MessageRecipientResolver : IMessageRecipientResolver
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
    private readonly IAppUserDirectoryReader _appUserDirectoryReader;
    private readonly IUserMessengerContactRepository _userMessengerContactRepository;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessagingAccessScope _accessScope;

    public MessageRecipientResolver(
        IMessagingService messagingService,
        IMessengerContactRepository messengerContactRepository,
        IOwnerMessengerReader ownerMessengerReader,
        IAppUserDirectoryReader appUserDirectoryReader,
        IUserMessengerContactRepository userMessengerContactRepository,
        IMessagingProviderRepository providerRepository,
        IMessagingAccessScope accessScope)
    {
        _messagingService = messagingService;
        _messengerContactRepository = messengerContactRepository;
        _ownerMessengerReader = ownerMessengerReader;
        _appUserDirectoryReader = appUserDirectoryReader;
        _userMessengerContactRepository = userMessengerContactRepository;
        _providerRepository = providerRepository;
        _accessScope = accessScope;
    }

    public async Task<MessageRecipientResolution> ResolveAsync(
        SendMessageParameters parameters,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (parameters.RecipientType == SendMessageParameters.RecipientTypeUser)
        {
            return isAdmin
                ? await ResolveUserAsync(parameters.Recipient, parameters.Provider, cancellationToken)
                : MessageRecipientResolution.Failed(MessagingConstants.SendRequiresAdministratorError);
        }

        if (parameters.RecipientType != SendMessageParameters.RecipientTypeClient)
        {
            return MessageRecipientResolution.Failed(
                $"Unknown recipientType '{parameters.RecipientType}'. Use 'client' or 'user'.");
        }

        if (!isAdmin && SelfAliases.Contains(parameters.Recipient.Trim()))
        {
            return MessageRecipientResolution.Failed(MessagingConstants.SendRequiresAdministratorError);
        }

        var provider = parameters.Provider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            var sole = await ResolveSoleEnabledProviderAsync();
            if (sole.Error != null)
            {
                return MessageRecipientResolution.Failed(sole.Error);
            }

            provider = sole.ProviderType!;
        }

        var lookup = await ResolveClientRecipientAsync(parameters.Recipient, provider, isAdmin, cancellationToken);
        if (lookup.Recipient == null)
        {
            return lookup.AmbiguousNames.Count > 1
                ? MessageRecipientResolution.Failed(
                    $"Multiple clients match '{parameters.Recipient}': {string.Join(", ", lookup.AmbiguousNames)}. Please specify which one.")
                : BuildNoRecipientError(parameters.Recipient, provider);
        }

        if (!isAdmin && !await IsRecipientOfVisibleClientAsync(provider, lookup.Recipient.Identifier, cancellationToken))
        {
            return BuildNoRecipientError(parameters.Recipient, provider);
        }

        return MessageRecipientResolution.Resolved(provider, lookup.Recipient.DisplayName, lookup.Recipient.Identifier);
    }

    /// <summary>
    /// Auto-resolves the provider when the caller omitted it: with exactly one enabled provider there
    /// is nothing to ask about, so the skill must not force a channel choice the installation cannot
    /// even offer. Returns the provider's type (e.g. "Slack"), not its admin-assigned Name, matching
    /// what ProviderToMessengerType and the downstream provider lookup both expect.
    /// </summary>
    private async Task<(string? ProviderType, string? Error)> ResolveSoleEnabledProviderAsync()
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

    private static MessageRecipientResolution BuildNoRecipientError(string recipient, string provider)
    {
        if (SelfAliases.Contains(recipient.Trim()))
        {
            return MessageRecipientResolution.Failed(
                string.Format(CultureInfo.InvariantCulture, MessagingConstants.OwnerAddressMissingErrorFormat, provider));
        }

        if (ProviderToMessengerType.TryGetValue(provider, out var messengerType))
        {
            return MessageRecipientResolution.Failed(
                string.Format(CultureInfo.InvariantCulture, MessagingConstants.ClientContactMissingErrorFormat, messengerType, recipient));
        }

        return MessageRecipientResolution.Failed(string.Format(CultureInfo.InvariantCulture, MessagingConstants.UnknownProviderErrorFormat, provider));
    }

    private async Task<bool> IsRecipientOfVisibleClientAsync(string provider, string identifier, CancellationToken ct)
    {
        var clientId = await _messagingService.ResolveRecipientClientIdAsync(provider, identifier, ct);
        return await _accessScope.IsClientVisibleAsync(clientId, ct);
    }

    private async Task<RecipientLookup> ResolveClientRecipientAsync(string recipient, string provider, bool isAdmin, CancellationToken ct)
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

        return await ResolveByClientNameAsync(trimmed, messengerType, isAdmin, ct);
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
    private async Task<RecipientLookup> ResolveByClientNameAsync(string nameQuery, MessengerType messengerType, bool isAdmin, CancellationToken ct)
    {
        var matches = await _messengerContactRepository.SearchByClientNameAsync(nameQuery, messengerType, ct);
        if (!isAdmin)
        {
            matches = await KeepVisibleMatchesAsync(matches, ct);
        }

        if (matches.Count == 0)
            return new RecipientLookup(null, Array.Empty<string>());

        if (matches.Count > 1)
            return new RecipientLookup(
                null, matches.Take(MessagingConstants.ClientNameCandidateLimit).Select(BuildClientDisplayName).ToList());

        var match = matches[0];
        return new RecipientLookup(new ResolvedRecipient(BuildClientDisplayName(match), match.Value.Trim()), Array.Empty<string>());
    }

    private async Task<IReadOnlyList<ClientMessengerMatch>> KeepVisibleMatchesAsync(
        IReadOnlyList<ClientMessengerMatch> matches, CancellationToken ct)
    {
        var visibleClientIds = new HashSet<Guid>();
        foreach (var clientId in matches.Select(match => match.ClientId).Distinct())
        {
            if (await _accessScope.IsClientVisibleAsync(clientId, ct))
            {
                visibleClientIds.Add(clientId);
            }
        }

        return matches.Where(match => visibleClientIds.Contains(match.ClientId)).ToList();
    }

    private async Task<MessageRecipientResolution> ResolveUserAsync(string recipient, string? provider, CancellationToken ct)
    {
        var candidates = await _appUserDirectoryReader.SearchByNameAsync(recipient.Trim(), ct);

        if (candidates.Count == 0)
        {
            return MessageRecipientResolution.Failed($"No active user found matching '{recipient}'.");
        }

        if (candidates.Count > 1)
        {
            return MessageRecipientResolution.Failed(
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
                return MessageRecipientResolution.Failed(string.Format(CultureInfo.InvariantCulture, MessagingConstants.UnknownProviderErrorFormat, provider));
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
                return MessageRecipientResolution.Failed(
                    $"'{displayName}' has a paired {channel.Type} channel, but no enabled {channel.Type} provider is configured.");
            }

            providerName = matchingProvider.Name;
        }

        return MessageRecipientResolution.Resolved(providerName, displayName, channel.Value);
    }

    private async Task<MessageRecipientResolution> BuildNoChannelErrorAsync(
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
        return MessageRecipientResolution.Failed(
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
