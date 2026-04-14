// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Default implementation of ITelegramRolloutTrigger. Resolves the scoped IOnboardingRolloutService
/// via IServiceScopeFactory so the background task owns its own DI scope.
/// </summary>
/// <param name="settingsReader">Plugin settings reader used to check the rollout-completed flag.</param>
/// <param name="scopeFactory">Factory for a fresh DI scope for the background task.</param>
/// <param name="logger">Logger for diagnostic output.</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Klacks.Plugin.Messaging.Application.Services;

public class TelegramRolloutTrigger : ITelegramRolloutTrigger
{
    private readonly IPluginSettingsReader _settingsReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramRolloutTrigger> _logger;

    public TelegramRolloutTrigger(
        IPluginSettingsReader settingsReader,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramRolloutTrigger> logger)
    {
        _settingsReader = settingsReader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task TriggerIfNewlyEnabledAsync(bool wasEnabledBefore, bool isEnabledNow, string botConfigJson)
    {
        if (!isEnabledNow || wasEnabledBefore)
            return;

        var completed = await _settingsReader.GetSettingAsync(TelegramOnboardingConstants.RolloutCompletedSettingKey);
        if (!string.IsNullOrWhiteSpace(completed))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var rollout = scope.ServiceProvider.GetRequiredService<IOnboardingRolloutService>();
                await rollout.ExecuteAsync(botConfigJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background Telegram onboarding rollout failed");
            }
        });
    }
}
