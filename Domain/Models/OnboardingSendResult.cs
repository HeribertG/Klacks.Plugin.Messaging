// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Result of attempting to send a Telegram onboarding invitation.
/// </summary>
namespace Klacks.Plugin.Messaging.Domain.Models;

public enum OnboardingSendResult
{
    Success,
    NotEmployee,
    NoContactChannel,
    AlreadyLinked,
    SendFailed
}
