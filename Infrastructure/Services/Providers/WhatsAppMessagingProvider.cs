// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// WhatsApp Cloud API messaging provider adapter (Meta Business Platform).
/// Requires a WhatsApp Business account and valid API token.
/// </summary>
/// <param name="httpClient">HTTP client for WhatsApp API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class WhatsAppMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderWhatsApp;

    public bool SupportsPhoneAsRecipient => true;

    public WhatsAppMessagingProvider(HttpClient httpClient, ILogger<WhatsAppMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("WhatsApp provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "WhatsApp provider is not yet configured"));
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
