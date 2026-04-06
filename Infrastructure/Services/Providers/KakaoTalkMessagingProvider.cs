// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// KakaoTalk messaging provider adapter (dominant messenger in South Korea).
/// Requires a Kakao Developers account and channel registration.
/// </summary>
/// <param name="httpClient">HTTP client for Kakao API requests</param>
/// <param name="logger">Logger instance</param>
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class KakaoTalkMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KakaoTalkMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderKakaoTalk;

    public KakaoTalkMessagingProvider(HttpClient httpClient, ILogger<KakaoTalkMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        _logger.LogWarning("KakaoTalk provider is not yet fully implemented");
        return Task.FromResult(new SendMessageResult(false, ErrorMessage: "KakaoTalk provider is not yet configured"));
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
