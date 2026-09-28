// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Result of an administrator inviting an AppUser to link Telegram by email.
/// </summary>
namespace Klacks.Plugin.Messaging.Domain.Models;

public enum UserInviteSendResult
{
    Success,
    UserNotFound,
    AlreadyLinked,
    NoEmail,
    SendFailed
}
