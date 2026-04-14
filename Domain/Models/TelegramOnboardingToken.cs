// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// One-time invitation token that lets an employee link their Telegram account to their Klacks client.
/// </summary>
/// <param name="Id">Primary key.</param>
/// <param name="Token">URL-safe random string embedded in the bot deep-link.</param>
/// <param name="ClientId">FK to the Klacks Client this invitation belongs to.</param>
/// <param name="ExpiresAt">UTC instant after which the token is no longer redeemable.</param>
/// <param name="UsedAt">UTC instant of successful redemption, or null if unused.</param>
/// <param name="RedeemedChatId">Telegram chat id recorded on redemption, for audit.</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public class TelegramOnboardingToken
{
    public Guid Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public Guid ClientId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public string? RedeemedChatId { get; set; }

    public bool IsDeleted { get; set; }
}
