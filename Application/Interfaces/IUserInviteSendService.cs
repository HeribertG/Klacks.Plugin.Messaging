// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Sends an admin-initiated Telegram pairing invite to an AppUser by email.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IUserInviteSendService
{
    /// <param name="targetUserId">AppUser to invite.</param>
    /// <param name="issuedByAdminId">AppUser id of the administrator triggering the invite, for audit.</param>
    /// <param name="botConfigJson">Telegram provider config JSON holding the bot token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UserInviteSendResult> SendAsync(
        string targetUserId,
        string issuedByAdminId,
        string botConfigJson,
        CancellationToken ct = default);
}
