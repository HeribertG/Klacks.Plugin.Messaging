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

            try
            {
                var result = await _sendService.SendAsync(employee.ClientId, botConfigJson, ct);
                if (result == OnboardingSendResult.Success)
                {
                    sentCount++;
                    _logger.LogInformation("Onboarding sent to client {ClientId}", employee.ClientId);
                }
                else
                {
                    _logger.LogInformation("Onboarding skipped for client {ClientId}: {Result}", employee.ClientId, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Onboarding failed for client {ClientId}", employee.ClientId);
            }

            try
            {
                await Task.Delay(TelegramOnboardingConstants.RolloutDelayMilliseconds, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        await _settingsWriter.SetSettingAsync(
            TelegramOnboardingConstants.RolloutCompletedSettingKey,
            DateTime.UtcNow.ToString("O"),
            ct);

        _logger.LogInformation("Telegram onboarding rollout complete. Sent: {Sent}/{Total}", sentCount, employees.Count);
        return sentCount;
    }
}
