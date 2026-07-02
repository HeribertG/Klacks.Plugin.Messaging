// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Slack messaging provider adapter using the Slack Web API and Events API.
/// Sends messages via chat.postMessage, validates incoming webhooks with the
/// Slack v0 request signature scheme (HMAC-SHA256) and parses event_callback payloads.
/// </summary>
/// <param name="httpClient">HTTP client for Slack Web API requests</param>
/// <param name="logger">Logger instance</param>
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class SlackMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackMessagingProvider> _logger;

    private const string PostMessageUrl = "https://slack.com/api/chat.postMessage";
    private const string AuthTestUrl = "https://slack.com/api/auth.test";
    private const string BearerScheme = "Bearer";
    private const string JsonContentType = "application/json";
    private const string SignatureHeader = "X-Slack-Signature";
    private const string TimestampHeader = "X-Slack-Request-Timestamp";
    private const string SignatureVersion = "v0";
    private const int MaxTimestampSkewSeconds = 300;
    private const string OkProperty = "ok";
    private const string ErrorProperty = "error";
    private const string TimestampProperty = "ts";
    private const string TypeProperty = "type";
    private const string EventProperty = "event";
    private const string UserProperty = "user";
    private const string TextProperty = "text";
    private const string BotIdProperty = "bot_id";
    private const string SubtypeProperty = "subtype";
    private const string ChallengeProperty = "challenge";
    private const string UrlVerificationType = "url_verification";
    private const string EventCallbackType = "event_callback";
    private const string MessageEventType = "message";
    private const string ErrorMissingBotToken = "Invalid Slack configuration: missing BotToken";
    private const string ErrorMissingChannel = "No Slack channel specified: recipient and DefaultChannel are both empty";
    private const string ErrorUnknown = "unknown";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderSlack;

    public bool SupportsPhoneAsRecipient => false;

    public SlackMessagingProvider(HttpClient httpClient, ILogger<SlackMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingBotToken);

        var channel = string.IsNullOrWhiteSpace(request.Recipient) ? config.DefaultChannel : request.Recipient;
        if (string.IsNullOrWhiteSpace(channel))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingChannel);

        var payload = new { channel, text = request.Content };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, PostMessageUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonContentType)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.BotToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Slack API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: $"Slack API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            if (!result.TryGetProperty(OkProperty, out var ok) || ok.ValueKind != JsonValueKind.True)
            {
                var error = GetStringProperty(result, ErrorProperty) ?? ErrorUnknown;
                _logger.LogWarning("Slack chat.postMessage failed: {Error}", error);
                return new SendMessageResult(false, ErrorMessage: $"Slack API error: {error}");
            }

            var messageId = GetStringProperty(result, TimestampProperty);
            return new SendMessageResult(true, ExternalMessageId: messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return false;

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AuthTestUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.BotToken);

            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return result.TryGetProperty(OkProperty, out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    public WebhookValidationResult ValidateWebhook(WebhookValidationContext context)
    {
        var config = DeserializeConfig(context.ConfigJson);
        if (config == null || string.IsNullOrWhiteSpace(config.SigningSecret))
        {
            _logger.LogWarning("Slack webhook validation failed: missing SigningSecret in configuration");
            return new WebhookValidationResult(false);
        }

        var signature = context.GetHeader(SignatureHeader);
        var timestampValue = context.GetHeader(TimestampHeader);
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestampValue))
        {
            _logger.LogWarning("Slack webhook validation failed: missing signature or timestamp header");
            return new WebhookValidationResult(false);
        }

        if (!long.TryParse(timestampValue, out var timestamp))
        {
            _logger.LogWarning("Slack webhook validation failed: invalid timestamp header");
            return new WebhookValidationResult(false);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > MaxTimestampSkewSeconds)
        {
            _logger.LogWarning("Slack webhook validation failed: request timestamp outside allowed window");
            return new WebhookValidationResult(false);
        }

        var expectedSignature = ComputeSignature(config.SigningSecret, timestampValue, context.Body);
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature));

        if (!isValid)
        {
            _logger.LogWarning("Slack webhook validation failed: signature mismatch");
            return new WebhookValidationResult(false);
        }

        return new WebhookValidationResult(true, TryGetUrlVerificationChallenge(context.Body));
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
            if (json.ValueKind != JsonValueKind.Object || GetStringProperty(json, TypeProperty) != EventCallbackType)
                return null;

            if (!json.TryGetProperty(EventProperty, out var eventElement) || eventElement.ValueKind != JsonValueKind.Object)
                return null;

            if (GetStringProperty(eventElement, TypeProperty) != MessageEventType)
                return null;

            if (eventElement.TryGetProperty(BotIdProperty, out _) || eventElement.TryGetProperty(SubtypeProperty, out _))
                return null;

            var user = GetStringProperty(eventElement, UserProperty);
            var text = GetStringProperty(eventElement, TextProperty);
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(text))
                return null;

            var messageId = GetStringProperty(eventElement, TimestampProperty) ?? string.Empty;
            return new IncomingMessage(messageId, user, user, text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputeSignature(string signingSecret, string timestamp, string body)
    {
        var baseString = $"{SignatureVersion}:{timestamp}:{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(baseString));
        return $"{SignatureVersion}={Convert.ToHexStringLower(hash)}";
    }

    private static string? TryGetUrlVerificationChallenge(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
            if (json.ValueKind == JsonValueKind.Object && GetStringProperty(json, TypeProperty) == UrlVerificationType)
                return GetStringProperty(json, ChallengeProperty);

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static SlackConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<SlackConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record SlackConfig
    {
        public string BotToken { get; init; } = string.Empty;
        public string SigningSecret { get; init; } = string.Empty;
        public string DefaultChannel { get; init; } = string.Empty;
        public string WebhookUrl { get; init; } = string.Empty;
    }
}
