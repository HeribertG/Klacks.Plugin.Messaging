// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Fetches inbound messages for every enabled provider whose adapter can poll, and persists them
/// through the same path the webhook route uses. A webhook needs a publicly reachable URL, which
/// an installation on a workstation or behind NAT does not have - without this service, inbound
/// traffic never arrives at all on such an installation.
/// Only providers that are enabled are polled, so the provider toggle is a real off switch for the
/// inbound direction too, unlike the webhook route.
/// </summary>
/// <param name="_serviceProvider">Service provider for creating a scope per polling round.</param>
/// <param name="_logger">Logger instance.</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.DTOs;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class InboundMessagePollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboundMessagePollingService> _logger;

    public InboundMessagePollingService(
        IServiceProvider serviceProvider,
        ILogger<InboundMessagePollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InboundMessagePollingService started");

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(MessagingConstants.InboundPollIntervalSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await PollAllProvidersAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error during inbound message polling");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }

        _logger.LogInformation("InboundMessagePollingService stopped");
    }

    private async Task PollAllProvidersAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var providerRepository = scope.ServiceProvider.GetRequiredService<IMessagingProviderRepository>();
        var adapterFactory = scope.ServiceProvider.GetRequiredService<MessagingProviderAdapterFactory>();

        var providers = await providerRepository.GetEnabledAsync();

        foreach (var provider in providers)
        {
            if (adapterFactory.Create(provider.ProviderType) is not IInboundMessagePoller poller)
            {
                continue;
            }

            await PollProviderAsync(scope.ServiceProvider, poller, provider, ct);
        }
    }

    private async Task PollProviderAsync(
        IServiceProvider scopedProvider,
        IInboundMessagePoller poller,
        MessagingProvider provider,
        CancellationToken ct)
    {
        var settingsReader = scopedProvider.GetRequiredService<IPluginSettingsReader>();
        var settingsWriter = scopedProvider.GetRequiredService<IPluginSettingsWriter>();
        var messagingService = scopedProvider.GetRequiredService<IMessagingService>();
        var eventBus = scopedProvider.GetRequiredService<IPluginEventBus>();

        var cursorKey = MessagingConstants.InboundPollCursorSettingPrefix + provider.Name;
        var cursor = await settingsReader.GetSettingAsync(cursorKey);

        var result = await poller.PollAsync(provider.ConfigJson, cursor, ct);

        foreach (var incoming in result.Messages)
        {
            var message = await messagingService.IngestInboundMessageAsync(provider.Name, incoming, ct);
            if (message == null)
            {
                continue;
            }

            await eventBus.BroadcastAsync(MessagingConstants.IncomingMessageEventType, new IncomingMessageDto
            {
                MessageId = message.Id,
                ProviderName = provider.Name,
                ProviderDisplayName = provider.DisplayName,
                Sender = message.Sender,
                SenderDisplayName = message.SenderDisplayName,
                Content = message.Content,
                ContentType = message.ContentType,
                Timestamp = message.Timestamp
            });
        }

        // Advance even when nothing was collected: the poller still reports the newest timestamp it
        // saw, and re-reading messages it already filtered out would repeat forever.
        if (!string.IsNullOrWhiteSpace(result.NextCursor) && result.NextCursor != cursor)
        {
            await settingsWriter.SetSettingAsync(cursorKey, result.NextCursor, ct);
        }

        if (result.Messages.Count > 0)
        {
            _logger.LogInformation(
                "Polled {Count} inbound message(s) from provider {Provider}", result.Messages.Count, provider.Name);
        }
    }
}
