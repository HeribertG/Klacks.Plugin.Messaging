// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Constants shared by Telegram onboarding components (token lifetime, command prefixes, settings keys).
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class TelegramOnboardingConstants
{
    public const int TokenLifetimeDays = 30;
    public const int TokenByteLength = 32;
    public const string StartCommandPrefix = "/start ";
    public const string RolloutCompletedSettingKey = "TelegramOnboardingRolloutCompletedAt";
    public const int RolloutDelayMilliseconds = 1000;

    public const string InvitationSubject = "Klacks Telegram invitation";
    public const string FallbackRecipientName = "there";
    public const string RedeemedContactDescription = "Telegram auto-onboarding";

    public const string InvitationBodyTemplate =
        "Hello {0},\n\n" +
        "You have been invited to link your Telegram account with Klacks.\n" +
        "Open this link on your phone and press START in Telegram:\n{1}\n\n" +
        "This invitation expires in {2} days.";
}
