// Copyright (c) Heribert Gasparoli Private. All rights reserved.

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
