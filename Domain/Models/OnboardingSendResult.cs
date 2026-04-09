// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Result of attempting to send a Telegram onboarding invitation SMS.
/// </summary>
namespace Klacks.Plugin.Messaging.Domain.Models;

public enum OnboardingSendResult
{
    Success,
    NotEmployee,
    NoPhone,
    AlreadyLinked,
    SendFailed
}
