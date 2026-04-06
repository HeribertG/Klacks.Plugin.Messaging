// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Microsoft Teams messaging provider adapter.
/// Requires an Azure Bot registration and Teams channel configuration.
/// </summary>
/// <param name="httpClient">HTTP client for Microsoft Graph / Bot Framework API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class TeamsMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamsMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderTeams;

    public TeamsMessagingProvider(HttpClient httpClient, ILogger<TeamsMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("Microsoft Teams provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "Microsoft Teams provider is not yet configured"));
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
