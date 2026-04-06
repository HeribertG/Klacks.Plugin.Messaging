// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// WeChat messaging provider adapter (dominant messenger in China).
/// Requires a WeChat Official Account and API credentials.
/// </summary>
/// <param name="httpClient">HTTP client for WeChat API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class WeChatMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeChatMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderWeChat;

    public WeChatMessagingProvider(HttpClient httpClient, ILogger<WeChatMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("WeChat provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "WeChat provider is not yet configured"));
    }

    public Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    public WebhookValidationResult ValidateWebhook(string body, string signature, string webhookSecret)
    {
        return new WebhookValidationResult(false);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        return null;
    }
}
