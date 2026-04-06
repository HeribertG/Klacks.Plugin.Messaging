// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Slack messaging provider adapter.
/// Requires a Slack App with Bot Token and appropriate OAuth scopes.
/// </summary>
/// <param name="httpClient">HTTP client for Slack Web API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class SlackMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderSlack;

    public SlackMessagingProvider(HttpClient httpClient, ILogger<SlackMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("Slack provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "Slack provider is not yet configured"));
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
