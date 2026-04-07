// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Zalo messaging provider adapter (dominant messenger in Vietnam).
/// Requires a Zalo Official Account and API credentials.
/// </summary>
/// <param name="httpClient">HTTP client for Zalo API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class ZaloMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZaloMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderZalo;

    public bool SupportsPhoneAsRecipient => false;

    public ZaloMessagingProvider(HttpClient httpClient, ILogger<ZaloMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("Zalo provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "Zalo provider is not yet configured"));
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
