// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Per-client Telegram onboarding dispatcher. Generates a one-time token, builds the
/// deep-link, persists the token and sends the invitation via email when a PrivateMail
/// address exists. SMS delivery is currently stubbed out and degrades to email.
/// </summary>
/// <param name="employeeReader">Reader for strict private contact channels of Employee clients.</param>
/// <param name="tokenRepository">Repository for TelegramOnboardingToken persistence.</param>
/// <param name="contactRepository">Repository for MessengerContact persistence.</param>
/// <param name="emailSender">Plugin-scoped transactional email sender bridge.</param>
/// <param name="telegramProvider">Telegram provider used to resolve the bot username via getMe.</param>
/// <param name="unitOfWork">Unit of work used to persist token changes.</param>
/// <param name="logger">Logger for diagnostic output.</param>

using System.Security.Cryptography;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Klacks.Plugin.Messaging.Infrastructure.Services.Providers;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class OnboardingSendService : IOnboardingSendService
{
    private readonly IEmployeeClientReader _employeeReader;
    private readonly ITelegramOnboardingTokenRepository _tokenRepository;
    private readonly IMessengerContactRepository _contactRepository;
    private readonly IPluginEmailSender _emailSender;
    private readonly TelegramMessagingProvider _telegramProvider;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingSendService> _logger;

    public OnboardingSendService(
        IEmployeeClientReader employeeReader,
        ITelegramOnboardingTokenRepository tokenRepository,
        IMessengerContactRepository contactRepository,
        IPluginEmailSender emailSender,
        TelegramMessagingProvider telegramProvider,
        IPluginUnitOfWork unitOfWork,
        ILogger<OnboardingSendService> logger)
    {
        _employeeReader = employeeReader;
        _tokenRepository = tokenRepository;
        _contactRepository = contactRepository;
        _emailSender = emailSender;
        _telegramProvider = telegramProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OnboardingSendResult> SendAsync(Guid clientId, string botConfigJson, CancellationToken ct = default)
    {
        var employee = await _employeeReader.GetEmployeeAsync(clientId, ct);
        if (employee == null)
            return OnboardingSendResult.NotEmployee;

        var existing = await _contactRepository.GetByClientAndTypeAsync(clientId, MessengerType.Telegram, ct);
        if (existing != null && !existing.IsDeleted)
            return OnboardingSendResult.AlreadyLinked;

        if (string.IsNullOrWhiteSpace(employee.PrivateCellPhone) && string.IsNullOrWhiteSpace(employee.PrivateEmail))
            return OnboardingSendResult.NoPhone;

        var botUsername = await _telegramProvider.GetBotUsernameAsync(botConfigJson, ct);
        if (string.IsNullOrWhiteSpace(botUsername))
        {
            _logger.LogWarning("Onboarding invitation aborted for client {ClientId} — bot username could not be resolved", clientId);
            return OnboardingSendResult.SendFailed;
        }

        await _tokenRepository.InvalidateAllForClientAsync(clientId, ct);

        var token = GenerateToken();
        var record = new TelegramOnboardingToken
        {
            Id = Guid.NewGuid(),
            Token = token,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(TelegramOnboardingConstants.TokenLifetimeDays),
            IsDeleted = false,
        };
        await _tokenRepository.AddAsync(record, ct);
        await _unitOfWork.CompleteAsync();

        var deepLink = $"https://t.me/{botUsername}?start={token}";
        var recipientName = string.IsNullOrWhiteSpace(employee.FirstName) ? "there" : employee.FirstName;
        var body = string.Format(
            TelegramOnboardingConstants.InvitationBodyTemplate,
            recipientName,
            deepLink,
            TelegramOnboardingConstants.TokenLifetimeDays);

        if (!string.IsNullOrWhiteSpace(employee.PrivateEmail))
        {
            var sent = await _emailSender.SendEmailAsync(
                employee.PrivateEmail,
                TelegramOnboardingConstants.InvitationSubject,
                body,
                ct);

            if (sent)
            {
                _logger.LogInformation("Telegram onboarding invitation sent to client {ClientId} via email", clientId);
                return OnboardingSendResult.Success;
            }

            _logger.LogWarning("Email dispatch failed for client {ClientId} onboarding invitation", clientId);
            return OnboardingSendResult.SendFailed;
        }

        _logger.LogWarning("Client {ClientId} has only a PrivateCellPhone — SMS channel not yet implemented", clientId);
        return OnboardingSendResult.SendFailed;
    }

    private static string GenerateToken()
    {
        var bytes = new byte[TelegramOnboardingConstants.TokenByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
