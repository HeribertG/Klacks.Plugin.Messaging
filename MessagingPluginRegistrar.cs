// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Registers all Messaging plugin DI services, EF Core model configurations, and assemblies.
/// Implements IPluginRegistrar for discovery by the host application.
/// </summary>

using System.Reflection;
using System.Threading.RateLimiting;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Infrastructure.Http;
using Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;
using Klacks.Plugin.Messaging.Infrastructure.Repositories;
using Klacks.Plugin.Messaging.Application.Services;
using Klacks.Plugin.Messaging.Infrastructure.Services;
using Klacks.Plugin.Messaging.Infrastructure.Services.Providers;
using Klacks.Plugin.Messaging.Skills;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klacks.Plugin.Messaging;

public class MessagingPluginRegistrar : IPluginRegistrar
{
    private const string AnonymousPartitionKey = "anonymous";

    public string PluginName => MessagingConstants.PluginName;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessagingProviderRepository, MessagingProviderRepository>();
        services.AddScoped<IMessengerContactRepository, MessengerContactRepository>();
        services.AddScoped<IUserMessengerContactRepository, UserMessengerContactRepository>();
        services.AddScoped<ITelegramOnboardingTokenRepository, TelegramOnboardingTokenRepository>();
        services.AddScoped<IOwnerMessengerReader, OwnerMessengerReader>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<MessagingProviderAdapterFactory>();

        services.AddTransient<RateLimitRetryHandler>();

        AddProviderHttpClient<TelegramMessagingProvider>(services);
        services.AddScoped<ITelegramBotMetadataProvider>(sp => sp.GetRequiredService<TelegramMessagingProvider>());
        AddProviderHttpClient<WhatsAppMessagingProvider>(services);
        AddProviderHttpClient<SignalMessagingProvider>(services);
        AddProviderHttpClient<SmsMessagingProvider>(services);
        AddProviderHttpClient<ThreemaMessagingProvider>(services);
        AddProviderHttpClient<ViberMessagingProvider>(services);
        AddProviderHttpClient<LineMessagingProvider>(services);
        AddProviderHttpClient<KakaoTalkMessagingProvider>(services);
        AddProviderHttpClient<WeChatMessagingProvider>(services);
        AddProviderHttpClient<ZaloMessagingProvider>(services);
        AddProviderHttpClient<TeamsMessagingProvider>(services);
        AddProviderHttpClient<SlackMessagingProvider>(services);

        services.AddScoped<IOnboardingSendService, OnboardingSendService>();
        services.AddScoped<IOnboardingRolloutService, OnboardingRolloutService>();
        services.AddScoped<ITelegramOnboardingRedemptionService, TelegramOnboardingRedemptionService>();
        services.AddScoped<ITelegramRolloutTrigger, TelegramRolloutTrigger>();
        services.AddScoped<IUserMessengerPairingCodeStore, SettingsUserMessengerPairingCodeStore>();
        services.AddScoped<IUserMessengerPairingService, UserMessengerPairingService>();
        services.AddScoped<IUserInviteSendService, UserInviteSendService>();

        services.AddScoped<SendMessageSkill>();
        services.AddScoped<ReadMessagesSkill>();
        services.AddScoped<ListMessagingProvidersSkill>();

        services.AddScoped<IPluginOperationalCheck, MessagingOperationalCheck>();

        AddWebhookRateLimiting(services, configuration);

        var messageRetentionEnabled = configuration.GetValue<bool>("BackgroundServices:MessageRetention", true);

        if (messageRetentionEnabled)
            services.AddHostedService<MessageRetentionService>();

        // Default ON: inert per round on any installation without an enabled provider whose adapter
        // can poll. Where a webhook is reachable, the poll simply finds nothing the webhook has not
        // already stored - IngestInboundMessageAsync deduplicates on ExternalMessageId.
        var inboundPollingEnabled = configuration.GetValue<bool>("BackgroundServices:InboundMessagePolling", true);

        if (inboundPollingEnabled)
            services.AddHostedService<InboundMessagePollingService>();
    }

    public void ConfigureDbModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new MessagingProviderConfiguration());
        modelBuilder.ApplyConfiguration(new MessengerContactConfiguration());
        modelBuilder.ApplyConfiguration(new UserMessengerContactConfiguration());
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

    private static void AddProviderHttpClient<TProvider>(IServiceCollection services)
        where TProvider : class
    {
        services.AddHttpClient<TProvider>().AddHttpMessageHandler<RateLimitRetryHandler>();
    }

    /// <summary>
    /// Adds the webhook route's rate limiting policy to the host's RateLimiterOptions. Mirrors how
    /// Klacks.Api's own MCP endpoint registers its policy from an extension method rather than
    /// Program.cs: AddPolicy only inserts an entry keyed by name, so registration order relative to
    /// the host's own AddRateLimiter call does not matter.
    /// </summary>
    private static void AddWebhookRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue(
            MessagingRateLimitConstants.SettingWebhookPermitLimit,
            MessagingRateLimitConstants.DefaultWebhookPermitLimit);

        services.Configure<RateLimiterOptions>(options =>
            options.AddPolicy(MessagingRateLimitConstants.WebhookPolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? AnonymousPartitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = MessagingRateLimitConstants.WebhookRateLimitWindow
                    })));
    }
}
