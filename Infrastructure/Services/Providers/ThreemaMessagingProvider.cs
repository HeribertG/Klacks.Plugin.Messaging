// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Threema messaging provider adapter (Swiss, end-to-end encrypted).
/// Requires a Threema Gateway account and API credentials.
/// </summary>
/// <param name="httpClient">HTTP client for Threema Gateway API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class ThreemaMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThreemaMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderThreema;

    public bool SupportsPhoneAsRecipient => false;

    public ThreemaMessagingProvider(HttpClient httpClient, ILogger<ThreemaMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("Threema provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "Threema provider is not yet configured"));
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
