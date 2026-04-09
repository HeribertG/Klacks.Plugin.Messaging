// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Handles redemption of Telegram onboarding tokens from incoming /start commands.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface ITelegramOnboardingRedemptionService
{
    /// <summary>
    /// Validates a token and, on success, creates the MessengerContact linking the chat-id to the target client.
    /// </summary>
    /// <param name="token">Opaque token received in the /start deep-link.</param>
    /// <param name="chatId">Telegram chat id extracted from the webhook payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OnboardingRedeemResult> RedeemAsync(string token, string chatId, CancellationToken ct = default);
}
