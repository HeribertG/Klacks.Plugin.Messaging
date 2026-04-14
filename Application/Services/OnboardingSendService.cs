// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Per-client Telegram onboarding dispatcher. Generates a one-time token, builds the
/// deep-link, persists the token and sends the invitation via email when a PrivateEmail
/// address exists.
/// </summary>

using System.Security.Cryptography;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class OnboardingSendService : IOnboardingSendService
{
    private readonly IEmployeeClientReader _employeeReader;
    private readonly ITelegramOnboardingTokenRepository _tokenRepository;
    private readonly IMessengerContactRepository _contactRepository;
    private readonly IPluginEmailSender _emailSender;
    private readonly ITelegramBotMetadataProvider _botMetadataProvider;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingSendService> _logger;

    public OnboardingSendService(
        IEmployeeClientReader employeeReader,
        ITelegramOnboardingTokenRepository tokenRepository,
        IMessengerContactRepository contactRepository,
        IPluginEmailSender emailSender,
        ITelegramBotMetadataProvider botMetadataProvider,
        IPluginUnitOfWork unitOfWork,
        ILogger<OnboardingSendService> logger)
    {
        _employeeReader = employeeReader;
        _tokenRepository = tokenRepository;
        _contactRepository = contactRepository;
        _emailSender = emailSender;
        _botMetadataProvider = botMetadataProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OnboardingSendResult> SendAsync(Guid clientId, string botConfigJson, CancellationToken ct = default)
    {
        var (employee, guardResult) = await ValidateCandidateAsync(clientId, ct);
        if (guardResult != null)
            return guardResult.Value;

        var botUsername = await _botMetadataProvider.GetBotUsernameAsync(botConfigJson, ct);
        if (string.IsNullOrWhiteSpace(botUsername))
        {
            _logger.LogWarning("Onboarding invitation aborted for client {ClientId} — bot username could not be resolved", clientId);
            return OnboardingSendResult.SendFailed;
        }

        var token = await CreateAndPersistTokenAsync(clientId, ct);
        var deepLink = $"https://t.me/{botUsername}?start={token}";
        return await DispatchInvitationAsync(clientId, employee!, deepLink, ct);
    }

    private async Task<(EmployeeClientInfo? Employee, OnboardingSendResult? Guard)> ValidateCandidateAsync(Guid clientId, CancellationToken ct)
    {
        var employee = await _employeeReader.GetEmployeeAsync(clientId, ct);
        if (employee == null)
            return (null, OnboardingSendResult.NotEmployee);

        var existing = await _contactRepository.GetByClientAndTypeAsync(clientId, MessengerType.Telegram, ct);
        if (existing != null && !existing.IsDeleted)
            return (employee, OnboardingSendResult.AlreadyLinked);

        if (string.IsNullOrWhiteSpace(employee.PrivateCellPhone) && string.IsNullOrWhiteSpace(employee.PrivateEmail))
            return (employee, OnboardingSendResult.NoContactChannel);

        return (employee, null);
    }

    private async Task<string> CreateAndPersistTokenAsync(Guid clientId, CancellationToken ct)
    {
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
        return token;
    }

    private async Task<OnboardingSendResult> DispatchInvitationAsync(
        Guid clientId,
        EmployeeClientInfo employee,
        string deepLink,
        CancellationToken ct)
    {
        var body = BuildInvitationBody(employee.FirstName, deepLink);

        if (string.IsNullOrWhiteSpace(employee.PrivateEmail))
        {
            _logger.LogWarning("Client {ClientId} has only a PrivateCellPhone — SMS channel not yet implemented", clientId);
            return OnboardingSendResult.SendFailed;
        }

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

    private static string BuildInvitationBody(string? firstName, string deepLink)
    {
        var recipientName = string.IsNullOrWhiteSpace(firstName)
            ? TelegramOnboardingConstants.FallbackRecipientName
            : firstName;

        return string.Format(
            TelegramOnboardingConstants.InvitationBodyTemplate,
            recipientName,
            deepLink,
            TelegramOnboardingConstants.TokenLifetimeDays);
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
