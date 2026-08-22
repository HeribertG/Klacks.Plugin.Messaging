// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Admin-initiated messenger pairing invite for an AppUser, provider-agnostic. Issues an admin-scoped
/// pairing code through IUserMessengerPairingService (which records issuedByAdminId for audit), asks
/// the resolved provider adapter for pairing instructions (e.g. a Telegram deep link, or plain Slack
/// instructions naming the bot), and emails them. A provider whose adapter does not implement
/// IPairingInstructionsProvider cannot be used here at all. Deliberately thin: the pairing invariants
/// themselves - who a code belongs to, when it expires, that a foreign account already holding the
/// channel refuses the link - all live in UserMessengerPairingService.
/// </summary>
/// <param name="userDirectory">Resolves the target AppUser's name and email.</param>
/// <param name="contactRepository">Checks whether the target already has a contact for this provider.</param>
/// <param name="pairingService">Issues the admin-scoped pairing code.</param>
/// <param name="emailSender">Sends the invitation email.</param>
/// <param name="adapterFactory">Resolves the provider adapter to build pairing instructions.</param>
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
    private readonly IMessagingProviderAdapterFactory _adapterFactory;
    private readonly ILogger<UserInviteSendService> _logger;

    public UserInviteSendService(
        IAppUserDirectoryReader userDirectory,
        IUserMessengerContactRepository contactRepository,
        IUserMessengerPairingService pairingService,
        IPluginEmailSender emailSender,
        IMessagingProviderAdapterFactory adapterFactory,
        ILogger<UserInviteSendService> logger)
    {
        _userDirectory = userDirectory;
        _contactRepository = contactRepository;
        _pairingService = pairingService;
        _emailSender = emailSender;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public async Task<UserInviteSendResult> SendAsync(
        string targetUserId,
        string issuedByAdminId,
        string providerType,
        string configJson,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<MessengerType>(providerType, ignoreCase: true, out var messengerType))
        {
            _logger.LogWarning(
                "Admin invite aborted for user {UserId} — cannot map provider type '{ProviderType}' to a messenger type",
                targetUserId, providerType);
            return UserInviteSendResult.SendFailed;
        }

        var user = await _userDirectory.GetUserAsync(targetUserId, ct);
        if (user == null)
            return UserInviteSendResult.UserNotFound;

        var existing = await _contactRepository.GetByUserAndTypeAsync(targetUserId, messengerType, ct);
        if (existing != null)
            return UserInviteSendResult.AlreadyLinked;

        if (string.IsNullOrWhiteSpace(user.Email))
            return UserInviteSendResult.NoEmail;

        var adapter = _adapterFactory.Create(providerType);
        if (adapter is not IPairingInstructionsProvider instructionsProvider)
        {
            _logger.LogWarning(
                "Admin invite aborted for user {UserId} — provider '{ProviderType}' does not support pairing instructions",
                targetUserId, providerType);
            return UserInviteSendResult.SendFailed;
        }

        var issued = await _pairingService.IssueAdminInviteAsync(targetUserId, issuedByAdminId, messengerType, ct);

        var instructions = await instructionsProvider.BuildPairingInstructionsAsync(issued.Code, configJson, ct);
        if (string.IsNullOrWhiteSpace(instructions))
        {
            _logger.LogWarning(
                "Admin invite aborted for user {UserId} — pairing instructions could not be resolved for provider '{ProviderType}'",
                targetUserId, providerType);
            return UserInviteSendResult.SendFailed;
        }

        var body = BuildInvitationBody(user.FirstName, instructions);

        var sent = await _emailSender.SendEmailAsync(user.Email, UserInviteConstants.InvitationSubject, body, ct);
        if (!sent)
        {
            _logger.LogWarning("Email dispatch failed for admin invite to user {UserId}", targetUserId);
            return UserInviteSendResult.SendFailed;
        }

        _logger.LogInformation(
            "Admin {AdminId} sent a {ProviderType} pairing invite to user {UserId}", issuedByAdminId, providerType, targetUserId);
        return UserInviteSendResult.Success;
    }

    private static string BuildInvitationBody(string? firstName, string instructions)
    {
        var recipientName = string.IsNullOrWhiteSpace(firstName)
            ? UserInviteConstants.FallbackRecipientName
            : firstName;

        return string.Format(
            UserInviteConstants.InvitationBodyTemplate,
            recipientName,
            instructions,
            UserMessengerPairingConstants.AdminInviteCodeLifetimeHours);
    }
}
