// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Microsoft Teams messaging provider adapter, outgoing-only via Teams workflow webhooks
/// (Power Automate "When a Teams webhook request is received" flow posting an Adaptive Card).
/// The recipient is ignored; every message is posted to the channel behind the configured
/// webhook URL. There is no incoming channel, so webhook validation and payload parsing
/// are permanently disabled.
/// </summary>
/// <param name="httpClient">HTTP client for posting Adaptive Card payloads to the webhook URL</param>
/// <param name="logger">Logger instance</param>
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class TeamsMessagingProvider : IMessagingProviderAdapter
{
    private const string MessageType = "message";
    private const string AdaptiveCardContentType = "application/vnd.microsoft.card.adaptive";
    private const string AdaptiveCardSchemaUrl = "http://adaptivecards.io/schemas/adaptive-card.json";
    private const string AdaptiveCardType = "AdaptiveCard";
    private const string AdaptiveCardVersion = "1.4";
    private const string TextBlockType = "TextBlock";
    private const string JsonMediaType = "application/json";
    private const string ConnectionTestMessage = "Klacks connection test";
    private const string InvalidConfigError = "Invalid Microsoft Teams configuration: WebhookUrl must be an absolute https URL";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamsMessagingProvider> _logger;

    public string ProviderType => MessagingConstants.ProviderTeams;

    public bool SupportsPhoneAsRecipient => false;

    public TeamsMessagingProvider(HttpClient httpClient, ILogger<TeamsMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || !IsValidWebhookUrl(config.WebhookUrl))
            return new SendMessageResult(false, ErrorMessage: InvalidConfigError);

        return await PostCardAsync(config.WebhookUrl, request.Content, ct);
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || !IsValidWebhookUrl(config.WebhookUrl))
            return false;

        var result = await PostCardAsync(config.WebhookUrl, ConnectionTestMessage, ct);
        return result.Success;
    }

    public WebhookValidationResult ValidateWebhook(WebhookValidationContext context)
    {
        return new WebhookValidationResult(false);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        return null;
    }

    private async Task<SendMessageResult> PostCardAsync(string webhookUrl, string text, CancellationToken ct)
    {
        var payload = BuildAdaptiveCardPayload(text);
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, JsonMediaType);

        try
        {
            var response = await _httpClient.PostAsync(webhookUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Microsoft Teams webhook error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: $"Microsoft Teams webhook error: {response.StatusCode}");
            }

            return new SendMessageResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Microsoft Teams message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    private static object BuildAdaptiveCardPayload(string text)
    {
        return new
        {
            type = MessageType,
            attachments = new object[]
            {
                new
                {
                    contentType = AdaptiveCardContentType,
                    content = new Dictionary<string, object>
                    {
                        ["$schema"] = AdaptiveCardSchemaUrl,
                        ["type"] = AdaptiveCardType,
                        ["version"] = AdaptiveCardVersion,
                        ["body"] = new object[]
                        {
                            new { type = TextBlockType, text, wrap = true }
                        }
                    }
                }
            }
        };
    }

    private static TeamsConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<TeamsConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValidWebhookUrl(string webhookUrl)
    {
        return !string.IsNullOrWhiteSpace(webhookUrl)
            && Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

    private record TeamsConfig
    {
        public string WebhookUrl { get; init; } = string.Empty;
    }
}
