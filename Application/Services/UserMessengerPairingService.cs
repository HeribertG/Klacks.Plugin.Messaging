// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// The writer UserMessengerContact never had. A Telegram chat id cannot be typed in by anyone -
/// neither the administrator nor the user themselves knows it - so a plain input form could not fill
/// that column correctly at all. The user therefore has a code issued, sends it to the bot, and the
/// inbound path writes the contact with the real sender identity it observed.
/// Built next to TelegramOnboardingRedemptionService instead of inside it: that service links a
/// Client through a non-nullable Guid, an AppUser key is a string, and merging both would turn 'who
/// may be linked' from a type distinction into a runtime discriminator on the very flow that must
/// never link a foreign account.
/// </summary>
/// <param name="codeStore">Issues and consumes the short-lived codes.</param>
/// <param name="contactRepository">Persists the resulting user-level messenger contact.</param>
/// <param name="unitOfWork">Commits the new contact before the code is burned.</param>
/// <param name="logger">Structured log of issued and redeemed codes.</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class UserMessengerPairingService : IUserMessengerPairingService
{
    private readonly IUserMessengerPairingCodeStore _codeStore;
    private readonly IUserMessengerContactRepository _contactRepository;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly ILogger<UserMessengerPairingService> _logger;

    public UserMessengerPairingService(
        IUserMessengerPairingCodeStore codeStore,
        IUserMessengerContactRepository contactRepository,
        IPluginUnitOfWork unitOfWork,
        ILogger<UserMessengerPairingService> logger)
    {
        _codeStore = codeStore;
        _contactRepository = contactRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UserMessengerPairingCode> IssueCodeAsync(string userId, MessengerType type, CancellationToken ct = default)
    {
        var issued = await _codeStore.IssueAsync(userId, type, ct);

        _logger.LogInformation(
            "Issued a messenger pairing code for user {UserId}, valid until {ExpiresAt}",
            userId,
            issued.ExpiresAt);

        return issued;
    }

    public async Task<UserMessengerPairingCode> IssueAdminInviteAsync(string targetUserId, string issuedByAdminId, MessengerType type, CancellationToken ct = default)
    {
        var issued = await _codeStore.IssueAdminInviteAsync(targetUserId, type, issuedByAdminId, ct);

        _logger.LogInformation(
            "Admin {AdminId} issued a messenger pairing invite for user {UserId}, valid until {ExpiresAt}",
            issuedByAdminId,
            targetUserId,
            issued.ExpiresAt);

        return issued;
    }

    public async Task<OnboardingRedeemResult> RedeemAsync(
        string code,
        MessengerType type,
        string senderValue,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(senderValue))
            return OnboardingRedeemResult.TokenNotFound;

        var lookup = await _codeStore.PeekAsync(code, type, ct);
        if (lookup.Result != OnboardingRedeemResult.Success || lookup.Code == null)
            return lookup.Result;

        var record = lookup.Code;
        var existing = await _contactRepository.GetByTypeAndValueAsync(type, senderValue, ct);

        if (existing != null)
        {
            return await HandleExistingContactAsync(existing, record, code, senderValue, ct);
        }

        await CreateContactAsync(record, senderValue, ct);
        await _codeStore.MarkUsedAsync(code, ct);

        _logger.LogInformation(
            "Paired {Type} identity with user {UserId}",
            type,
            record.UserId);

        return OnboardingRedeemResult.Success;
    }

    /// <summary>
    /// A channel that is already linked is not linked twice. For its own owner that is a success -
    /// the requested state is in place and the code is spent - while for a different owner it is a
    /// refusal, because moving the channel would hand that account's notifications to the sender.
    /// </summary>
    private async Task<OnboardingRedeemResult> HandleExistingContactAsync(
        UserMessengerContact existing,
        UserMessengerPairingCode record,
        string code,
        string senderValue,
        CancellationToken ct)
    {
        if (!string.Equals(existing.UserId, record.UserId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Refused a pairing code for user {UserId}: the sending identity already belongs to another account",
                record.UserId);
            return OnboardingRedeemResult.ContactAlreadyLinked;
        }

        await _codeStore.MarkUsedAsync(code, ct);

        _logger.LogInformation(
            "Pairing code for user {UserId} matched a channel that was already linked; nothing was added",
            record.UserId);

        return OnboardingRedeemResult.Success;
    }

    /// <summary>
    /// The contact is committed before the code is marked used. Should the code write fail after
    /// that, the user simply redeems again and lands in the already-linked branch above, which is
    /// harmless - whereas burning the code first would cost them the channel on any later failure.
    /// </summary>
    private async Task CreateContactAsync(UserMessengerPairingCode record, string senderValue, CancellationToken ct)
    {
        var channels = await _contactRepository.GetByUserIdAsync(record.UserId, ct);

        var contact = new UserMessengerContact
        {
            Id = Guid.NewGuid(),
            UserId = record.UserId,
            Type = record.Type,
            Value = senderValue,
            Description = UserMessengerPairingConstants.PairedContactDescription,
            IsPreferred = channels.Count == 0,
            IsDeleted = false,
            CreateTime = DateTime.UtcNow
        };

        await _contactRepository.AddAsync(contact, ct);
        await _unitOfWork.CompleteAsync();
    }
}
