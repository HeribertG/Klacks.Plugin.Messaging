// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants shared by Telegram onboarding components (token lifetime, command prefixes, settings keys).
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class TelegramOnboardingConstants
{
    public const int TokenLifetimeDays = 30;
    public const int TokenByteLength = 32;
    public const string StartCommandPrefix = "/start ";
    public const string StartCommand = "/start";
    public const string ProviderName = "telegram";
    public const string RolloutCompletedSettingKey = "TelegramOnboardingRolloutCompletedAt";
    public const int RolloutDelayMilliseconds = 1000;
}
