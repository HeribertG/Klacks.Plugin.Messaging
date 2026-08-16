// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Result of attempting to redeem a Telegram onboarding token or a user messenger pairing code.
/// </summary>
namespace Klacks.Plugin.Messaging.Domain.Models;

public enum OnboardingRedeemResult
{
    Success,
    TokenNotFound,
    TokenExpired,
    TokenAlreadyUsed,

    /// <summary>
    /// The messenger identity that redeemed the code is already linked to a different account.
    /// Refused rather than moved: a chat id that silently changes owner would let whoever holds it
    /// take over the notifications of the account it belonged to before.
    /// </summary>
    ContactAlreadyLinked
}
