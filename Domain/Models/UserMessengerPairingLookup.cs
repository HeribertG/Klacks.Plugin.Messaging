// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Outcome of looking a pairing code up in the store. Reuses OnboardingRedeemResult so an unknown,
/// expired and already used code stay the three distinguishable cases the employee onboarding flow
/// already names, instead of inventing a second vocabulary for the same three states.
/// </summary>
/// <param name="Result">Why the lookup succeeded or failed</param>
/// <param name="Code">The matched record; null unless Result is Success</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public record UserMessengerPairingLookup(OnboardingRedeemResult Result, UserMessengerPairingCode? Code = null)
{
    public static UserMessengerPairingLookup NotFound { get; } = new(OnboardingRedeemResult.TokenNotFound);

    public static UserMessengerPairingLookup Expired { get; } = new(OnboardingRedeemResult.TokenExpired);

    public static UserMessengerPairingLookup AlreadyUsed { get; } = new(OnboardingRedeemResult.TokenAlreadyUsed);

    public static UserMessengerPairingLookup Success(UserMessengerPairingCode code) =>
        new(OnboardingRedeemResult.Success, code);
}
