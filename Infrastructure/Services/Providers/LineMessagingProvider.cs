// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// LINE messaging provider adapter (popular in Japan, Taiwan, Thailand).
/// Requires a LINE Official Account and Messaging API channel.
/// </summary>
/// <param name="httpClient">HTTP client for LINE Messaging API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class LineMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LineMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderLine;

    public LineMessagingProvider(HttpClient httpClient, ILogger<LineMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("LINE provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "LINE provider is not yet configured"));
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
