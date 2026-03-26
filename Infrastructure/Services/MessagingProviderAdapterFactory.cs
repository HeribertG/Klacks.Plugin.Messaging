// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Factory for creating messaging provider adapters based on provider type.
/// </summary>
/// <param name="providerType">The provider type constant from MessagingConstants</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class MessagingProviderAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MessagingProviderAdapterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMessagingProviderAdapter Create(string providerType)
    {
        return providerType switch
        {
            MessagingConstants.ProviderTelegram => _serviceProvider.GetRequiredService<TelegramMessagingProvider>(),
            MessagingConstants.ProviderWhatsApp => _serviceProvider.GetRequiredService<WhatsAppMessagingProvider>(),
            MessagingConstants.ProviderSignal => _serviceProvider.GetRequiredService<SignalMessagingProvider>(),
            MessagingConstants.ProviderSms => _serviceProvider.GetRequiredService<SmsMessagingProvider>(),
            _ => throw new ArgumentException($"Unknown messaging provider type: {providerType}")
        };
    }
}
