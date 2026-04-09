// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Registers all Messaging plugin DI services, EF Core model configurations, and assemblies.
/// Implements IPluginRegistrar for discovery by the host application.
/// </summary>

using System.Reflection;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;
using Klacks.Plugin.Messaging.Infrastructure.Repositories;
using Klacks.Plugin.Messaging.Application.Services;
using Klacks.Plugin.Messaging.Infrastructure.Services;
using Klacks.Plugin.Messaging.Infrastructure.Services.Providers;
using Klacks.Plugin.Messaging.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klacks.Plugin.Messaging;

public class MessagingPluginRegistrar : IPluginRegistrar
{
    public string PluginName => MessagingConstants.PluginName;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessagingProviderRepository, MessagingProviderRepository>();
        services.AddScoped<IMessengerContactRepository, MessengerContactRepository>();
        services.AddScoped<ITelegramOnboardingTokenRepository, TelegramOnboardingTokenRepository>();
        services.AddScoped<IOwnerMessengerReader, OwnerMessengerReader>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<MessagingProviderAdapterFactory>();

        services.AddHttpClient<TelegramMessagingProvider>();
        services.AddHttpClient<WhatsAppMessagingProvider>();
        services.AddHttpClient<SignalMessagingProvider>();
        services.AddHttpClient<SmsMessagingProvider>();
        services.AddHttpClient<ThreemaMessagingProvider>();
        services.AddHttpClient<ViberMessagingProvider>();
        services.AddHttpClient<LineMessagingProvider>();
        services.AddHttpClient<KakaoTalkMessagingProvider>();
        services.AddHttpClient<WeChatMessagingProvider>();
        services.AddHttpClient<ZaloMessagingProvider>();
        services.AddHttpClient<TeamsMessagingProvider>();
        services.AddHttpClient<SlackMessagingProvider>();

        services.AddScoped<IOnboardingSendService, OnboardingSendService>();

        services.AddScoped<SendMessageSkill>();
        services.AddScoped<ReadMessagesSkill>();
        services.AddScoped<ListMessagingProvidersSkill>();

        services.AddScoped<IPluginOperationalCheck, MessagingOperationalCheck>();

        var messageRetentionEnabled = configuration.GetValue<bool>("BackgroundServices:MessageRetention", true);

        if (messageRetentionEnabled)
            services.AddHostedService<MessageRetentionService>();
    }

    public void ConfigureDbModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new MessagingProviderConfiguration());
        modelBuilder.ApplyConfiguration(new MessengerContactConfiguration());
        modelBuilder.ApplyConfiguration(new TelegramOnboardingTokenConfiguration());
    }

    public IEnumerable<Assembly> GetControllerAssemblies()
    {
        yield return typeof(MessagingPluginRegistrar).Assembly;
    }

    public IEnumerable<Assembly> GetSkillAssemblies()
    {
        yield return typeof(MessagingPluginRegistrar).Assembly;
    }
}
