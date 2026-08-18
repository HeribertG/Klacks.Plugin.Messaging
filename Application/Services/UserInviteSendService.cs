// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Admin-initiated Telegram pairing invite for an AppUser. Issues an admin-scoped pairing code
/// through IUserMessengerPairingService (which records issuedByAdminId for audit), builds the same
/// Telegram deep-link the self-service and Employee-onboarding flows use, and emails it. Deliberately
/// thin: the pairing invariants themselves - who a code belongs to, when it expires, that a foreign
/// account already holding the channel refuses the link - all live in UserMessengerPairingService.
/// </summary>
/// <param name="userDirectory">Resolves the target AppUser's name and email.</param>
/// <param name="contactRepository">Checks whether the target already has a Telegram contact.</param>
/// <param name="pairingService">Issues the admin-scoped pairing code.</param>
/// <param name="emailSender">Sends the invitation email.</param>
/// <param name="botMetadataProvider">Resolves the bot username for the deep-link.</param>
/// <param name="logger">Structured log of invites sent and refused.</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class UserInviteSendService : IUserInviteSendService
{
    private readonly IAppUserDirectoryReader _userDirectory;
    private readonly IUserMessengerContactRepository _contactRepository;
    private readonly IUserMessengerPairingService _pairingService;
    private readonly IPluginEmailSender _emailSender;
    private readonly ITelegramBotMetadataProvider _botMetadataProvider;
    private readonly ILogger<UserInviteSendService> _logger;

    public UserInviteSendService(
        IAppUserDirectoryReader userDirectory,
        IUserMessengerContactRepository contactRepository,
        IUserMessengerPairingService pairingService,
        IPluginEmailSender emailSender,
        ITelegramBotMetadataProvider botMetadataProvider,
        ILogger<UserInviteSendService> logger)
    {
        _userDirectory = userDirectory;
        _contactRepository = contactRepository;
        _pairingService = pairingService;
        _emailSender = emailSender;
        _botMetadataProvider = botMetadataProvider;
        _logger = logger;
    }

    public async Task<UserInviteSendResult> SendAsync(
        string targetUserId,
        string issuedByAdminId,
        string botConfigJson,
        CancellationToken ct = default)
    {
        var user = await _userDirectory.GetUserAsync(targetUserId, ct);
        if (user == null)
            return UserInviteSendResult.UserNotFound;

        var existing = await _contactRepository.GetByUserAndTypeAsync(targetUserId, MessengerType.Telegram, ct);
        if (existing != null)
            return UserInviteSendResult.AlreadyLinked;

        if (string.IsNullOrWhiteSpace(user.Email))
            return UserInviteSendResult.NoEmail;

        var botUsername = await _botMetadataProvider.GetBotUsernameAsync(botConfigJson, ct);
        if (string.IsNullOrWhiteSpace(botUsername))
        {
            _logger.LogWarning("Admin invite aborted for user {UserId} — bot username could not be resolved", targetUserId);
            return UserInviteSendResult.SendFailed;
        }

        var issued = await _pairingService.IssueAdminInviteAsync(targetUserId, issuedByAdminId, ct);
        var deepLink = $"https://t.me/{botUsername}?start={issued.Code}";
        var body = BuildInvitationBody(user.FirstName, deepLink);

        var sent = await _emailSender.SendEmailAsync(user.Email, UserInviteConstants.InvitationSubject, body, ct);
        if (!sent)
        {
            _logger.LogWarning("Email dispatch failed for admin invite to user {UserId}", targetUserId);
            return UserInviteSendResult.SendFailed;
        }

        _logger.LogInformation("Admin {AdminId} sent a Telegram pairing invite to user {UserId}", issuedByAdminId, targetUserId);
        return UserInviteSendResult.Success;
    }

    private static string BuildInvitationBody(string? firstName, string deepLink)
    {
        var recipientName = string.IsNullOrWhiteSpace(firstName)
            ? UserInviteConstants.FallbackRecipientName
            : firstName;

        return string.Format(
            UserInviteConstants.InvitationBodyTemplate,
            recipientName,
            deepLink,
            UserMessengerPairingConstants.AdminInviteCodeLifetimeHours);
    }
}
