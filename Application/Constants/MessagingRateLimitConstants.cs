// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants governing rate-limit handling for outbound messaging: retry attempts on
/// throttled responses, backoff bounds and the pacing interval between broadcast sends.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class MessagingRateLimitConstants
{
    /// <summary>
    /// Maximum number of additional attempts after a throttled response. Kept low on purpose:
    /// broadcasts send once per recipient, so every retry multiplies across the whole run.
    /// </summary>
    public const int MaxRetryAttempts = 2;

    /// <summary>
    /// Base delay used for exponential backoff when the provider sends no Retry-After header.
    /// </summary>
    public const int BaseBackoffMilliseconds = 500;

    /// <summary>
    /// Upper bound for a single wait. Providers may send a Retry-After far beyond any
    /// acceptable request duration; waiting that long inside a synchronous request would
    /// stall the caller, so the handler gives up instead. Together with MaxRetryAttempts this
    /// bounds the worst case at 10 seconds of waiting per recipient.
    /// </summary>
    public const int MaxBackoffMilliseconds = 5000;

    /// <summary>
    /// Pacing interval inserted between individual sends of a broadcast. Deliberately small:
    /// broadcasts run inside the HTTP request, so this smooths bursts without stalling the
    /// caller. Hard throttling is absorbed by the retry handler honouring Retry-After; no
    /// additional pause is added here, because by the time a send reports back as throttled
    /// the handler has already waited out its retries.
    /// </summary>
    public const int DefaultBroadcastPacingMilliseconds = 100;

    /// <summary>
    /// Total time budget for one broadcast. Sends run inside the HTTP request, and a throttling
    /// provider can cost up to ten seconds per recipient, so an unbounded run would hit the
    /// gateway timeout with no record of what was delivered. When the budget is spent the run
    /// stops and the remaining recipients are logged as failed with an explicit reason.
    /// </summary>
    public const int MaxBroadcastDurationSeconds = 120;

    /// <summary>
    /// Error recorded for recipients that were never attempted because the budget ran out.
    /// </summary>
    public const string BroadcastBudgetExceededError = "Broadcast stopped: time budget exceeded before this recipient was reached";

    /// <summary>
    /// Settings key allowing the pacing interval to be tuned per installation without a deploy.
    /// </summary>
    public const string SettingBroadcastPacingMs = "MESSAGING_BROADCAST_PACING_MS";

    /// <summary>
    /// Upper bound accepted for the configurable pacing interval, so a misconfigured value
    /// cannot turn a broadcast into an endless request.
    /// </summary>
    public const int MaxBroadcastPacingMilliseconds = 5000;
}
