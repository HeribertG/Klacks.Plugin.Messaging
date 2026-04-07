// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Viber messaging provider adapter (popular in Eastern Europe and Middle East).
/// Requires a Viber Bot account and authentication token.
/// </summary>
/// <param name="httpClient">HTTP client for Viber API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class ViberMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ViberMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderViber;

    public bool SupportsPhoneAsRecipient => true;

    public ViberMessagingProvider(HttpClient httpClient, ILogger<ViberMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("Viber provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "Viber provider is not yet configured"));
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
