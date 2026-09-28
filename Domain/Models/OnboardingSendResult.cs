// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

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
