// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Issues and redeems the codes that link a messenger identity to an application user.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IUserMessengerPairingService
{
    /// <summary>
    /// Issues a self-service code for the given user and messenger. Callable only from the route
    /// that derives userId from the caller's own access token - a code for somebody else cannot be
    /// expressed through this method.
    /// </summary>
    Task<UserMessengerPairingCode> IssueCodeAsync(string userId, MessengerType type, CancellationToken ct = default);

    /// <summary>
    /// Issues a code for a different account on an administrator's behalf, so an admin can nudge a
    /// user to pair a messenger instead of only being able to point them at their own profile. This
    /// is the one deliberate exception to "a code can only be issued for the caller": it exists
    /// solely for the Admin-only invite endpoint and always records issuedByAdminId for audit.
    /// </summary>
    Task<UserMessengerPairingCode> IssueAdminInviteAsync(string targetUserId, string issuedByAdminId, MessengerType type, CancellationToken ct = default);

    /// <summary>
    /// Redeems a code against the identity that sent it and creates the messenger contact.
    /// </summary>
    /// <param name="code">The code the user sent to the bot.</param>
    /// <param name="type">Messenger the code arrived on.</param>
    /// <param name="senderValue">Provider-specific sender identifier, e.g. the Telegram chat id.</param>
    Task<OnboardingRedeemResult> RedeemAsync(
        string code,
        MessengerType type,
        string senderValue,
        CancellationToken ct = default);
}
