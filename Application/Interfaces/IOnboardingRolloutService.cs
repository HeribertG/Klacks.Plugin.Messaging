// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Bulk sends Telegram onboarding invitations to all Employee clients exactly once.
/// Marks completion in plugin settings to prevent re-runs.
/// </summary>

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IOnboardingRolloutService
{
    /// <summary>
    /// Iterates all Employee clients and dispatches an onboarding invitation for each,
    /// then persists the completion timestamp.
    /// </summary>
    /// <param name="botConfigJson">Telegram provider config JSON holding the bot token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<int> ExecuteAsync(string botConfigJson, CancellationToken ct = default);
}
