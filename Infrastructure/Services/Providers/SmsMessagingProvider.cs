// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// SMS messaging provider adapter for sending text messages via an SMS gateway API.
/// Requires a configured SMS gateway endpoint and API credentials.
/// </summary>
/// <param name="httpClient">HTTP client for SMS gateway API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class SmsMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmsMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderSms;

    public SmsMessagingProvider(HttpClient httpClient, ILogger<SmsMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("SMS provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "SMS provider is not yet configured"));
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
