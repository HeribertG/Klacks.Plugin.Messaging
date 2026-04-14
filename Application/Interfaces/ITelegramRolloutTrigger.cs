// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Fires the bulk Telegram onboarding rollout the first time a Telegram provider transitions from
/// disabled to enabled. Encapsulates the background-scope handling and the rollout-completed guard.
/// </summary>

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface ITelegramRolloutTrigger
{
    /// <summary>
    /// Kicks off a one-shot rollout when the provider transitions from disabled to enabled.
    /// Does nothing if the rollout has already been run for this host (tracked via plugin settings).
    /// Executes asynchronously on a background task; returns as soon as it is scheduled.
    /// </summary>
    /// <param name="wasEnabledBefore">True if the Telegram provider was enabled prior to the update.</param>
    /// <param name="isEnabledNow">True if the Telegram provider is enabled after the update.</param>
    /// <param name="botConfigJson">Telegram provider config JSON holding the bot token.</param>
    Task TriggerIfNewlyEnabledAsync(bool wasEnabledBefore, bool isEnabledNow, string botConfigJson);
}
