// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Result of attempting to redeem a Telegram onboarding token.
/// </summary>
namespace Klacks.Plugin.Messaging.Domain.Models;

public enum OnboardingRedeemResult
{
    Success,
    TokenNotFound,
    TokenExpired,
    TokenAlreadyUsed
}
