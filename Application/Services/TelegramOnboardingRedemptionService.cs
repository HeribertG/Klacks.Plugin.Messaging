// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Validates and redeems Telegram onboarding tokens, linking the inbound chat-id
/// to the target client via a new MessengerContact. Marks the token as used on success.
/// </summary>
/// <param name="tokenRepository">Repository for TelegramOnboardingToken persistence.</param>
/// <param name="contactRepository">Repository for MessengerContact persistence.</param>
/// <param name="unitOfWork">Unit of work used to persist redemption changes.</param>
/// <param name="logger">Logger for diagnostic output.</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class TelegramOnboardingRedemptionService : ITelegramOnboardingRedemptionService
{
    private readonly ITelegramOnboardingTokenRepository _tokenRepository;
    private readonly IMessengerContactRepository _contactRepository;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly ILogger<TelegramOnboardingRedemptionService> _logger;

    public TelegramOnboardingRedemptionService(
        ITelegramOnboardingTokenRepository tokenRepository,
        IMessengerContactRepository contactRepository,
        IPluginUnitOfWork unitOfWork,
        ILogger<TelegramOnboardingRedemptionService> logger)
    {
        _tokenRepository = tokenRepository;
        _contactRepository = contactRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OnboardingRedeemResult> RedeemAsync(string token, string chatId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
            return OnboardingRedeemResult.TokenNotFound;

        var record = await _tokenRepository.GetByTokenAsync(token, ct);
        if (record == null)
            return OnboardingRedeemResult.TokenNotFound;
        if (record.UsedAt != null)
            return OnboardingRedeemResult.TokenAlreadyUsed;
        if (record.ExpiresAt < DateTime.UtcNow)
            return OnboardingRedeemResult.TokenExpired;

        var contact = new MessengerContact
        {
            Id = Guid.NewGuid(),
            ClientId = record.ClientId,
            Type = MessengerType.Telegram,
            Value = chatId,
            Description = "Telegram auto-onboarding",
            IsDeleted = false,
            CreateTime = DateTime.UtcNow,
        };
        await _contactRepository.AddAsync(contact, ct);

        record.UsedAt = DateTime.UtcNow;
        record.RedeemedChatId = chatId;
        await _tokenRepository.UpdateAsync(record, ct);

        await _unitOfWork.CompleteAsync();

        _logger.LogInformation(
            "Redeemed Telegram onboarding token for client {ClientId}, chat {ChatId}",
            record.ClientId,
            chatId);

        return OnboardingRedeemResult.Success;
    }
}
