// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Bulk rollout service that sends Telegram onboarding invitations to every Employee client once,
/// rate-limited between invocations, and persists a completion timestamp to prevent repeat runs.
/// </summary>
/// <param name="employeeReader">Reader for the set of Employee clients.</param>
/// <param name="sendService">Per-client invitation dispatcher.</param>
/// <param name="settingsWriter">Plugin settings writer used to persist the completion timestamp.</param>
/// <param name="logger">Logger for diagnostic output.</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class OnboardingRolloutService : IOnboardingRolloutService
{
    private readonly IEmployeeClientReader _employeeReader;
    private readonly IOnboardingSendService _sendService;
    private readonly IPluginSettingsWriter _settingsWriter;
    private readonly ILogger<OnboardingRolloutService> _logger;

    public OnboardingRolloutService(
        IEmployeeClientReader employeeReader,
        IOnboardingSendService sendService,
        IPluginSettingsWriter settingsWriter,
        ILogger<OnboardingRolloutService> logger)
    {
        _employeeReader = employeeReader;
        _sendService = sendService;
        _settingsWriter = settingsWriter;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string botConfigJson, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Telegram onboarding rollout");

        var employees = await _employeeReader.GetAllEmployeesAsync(ct);
        _logger.LogInformation("Rollout target: {Count} employees", employees.Count);

        var sentCount = 0;
        foreach (var employee in employees)
        {
            if (ct.IsCancellationRequested)
                break;

            var result = await TrySendAsync(employee.ClientId, botConfigJson, ct);
            if (result == OnboardingSendResult.Success)
                sentCount++;

            if (!ShouldDelayAfter(result))
                continue;

            if (!await DelayAsync(ct))
                break;
        }

        await _settingsWriter.SetSettingAsync(
            TelegramOnboardingConstants.RolloutCompletedSettingKey,
            DateTime.UtcNow.ToString("O"),
            ct);

        _logger.LogInformation("Telegram onboarding rollout complete. Sent: {Sent}/{Total}", sentCount, employees.Count);
        return sentCount;
    }

    private async Task<OnboardingSendResult?> TrySendAsync(Guid clientId, string botConfigJson, CancellationToken ct)
    {
        try
        {
            var result = await _sendService.SendAsync(clientId, botConfigJson, ct);
            if (result == OnboardingSendResult.Success)
                _logger.LogInformation("Onboarding sent to client {ClientId}", clientId);
            else
                _logger.LogInformation("Onboarding skipped for client {ClientId}: {Result}", clientId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Onboarding failed for client {ClientId}", clientId);
            return null;
        }
    }

    private static bool ShouldDelayAfter(OnboardingSendResult? result)
        => result is OnboardingSendResult.Success or OnboardingSendResult.SendFailed;

    private static async Task<bool> DelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TelegramOnboardingConstants.RolloutDelayMilliseconds, ct);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
