// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Sends an admin-initiated messenger pairing invite to an AppUser by email. Provider-agnostic: the
/// target provider must implement IPairingInstructionsProvider, e.g. Telegram (a deep link) or Slack
/// (plain instructions naming the bot) - a provider implementing neither cannot be used here.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IUserInviteSendService
{
    /// <param name="targetUserId">AppUser to invite.</param>
    /// <param name="issuedByAdminId">AppUser id of the administrator triggering the invite, for audit.</param>
    /// <param name="providerType">The provider's type string (e.g. "Telegram", "Slack"), used to resolve the adapter and the MessengerType.</param>
    /// <param name="configJson">Provider config JSON holding the credentials needed to build pairing instructions.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UserInviteSendResult> SendAsync(
        string targetUserId,
        string issuedByAdminId,
        string providerType,
        string configJson,
        CancellationToken ct = default);
}
