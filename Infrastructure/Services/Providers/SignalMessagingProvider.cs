// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Signal messaging provider adapter via signal-cli REST API.
/// Requires a running signal-cli instance with a registered phone number.
/// </summary>
/// <param name="httpClient">HTTP client for Signal API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class SignalMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SignalMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderSignal;

    public SignalMessagingProvider(HttpClient httpClient, ILogger<SignalMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("Signal provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "Signal provider is not yet configured"));
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
