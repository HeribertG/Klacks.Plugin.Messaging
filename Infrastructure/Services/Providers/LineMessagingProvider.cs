// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// LINE messaging provider adapter (popular in Japan, Taiwan, Thailand) using the LINE Messaging API.
/// Sends push messages via /v2/bot/message/push, validates incoming webhooks with the
/// x-line-signature scheme (Base64-encoded HMAC-SHA256 over the raw request body) and parses
/// text message events from webhook payloads. The webhook URL is not registered programmatically;
/// it must be entered manually in the LINE Developers Console.
/// </summary>
/// <param name="httpClient">HTTP client for LINE Messaging API requests</param>
/// <param name="logger">Logger instance</param>
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class LineMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LineMessagingProvider> _logger;

    private const string PushMessageUrl = "https://api.line.me/v2/bot/message/push";
    private const string BotInfoUrl = "https://api.line.me/v2/bot/info";
    private const string BearerScheme = "Bearer";
    private const string JsonContentType = "application/json";
    private const string SignatureHeader = "x-line-signature";
    private const string MessageEventType = "message";
    private const string MessageTypeText = "text";
    private const string PropSentMessages = "sentMessages";
    private const string PropEvents = "events";
    private const string PropType = "type";
    private const string PropMessage = "message";
    private const string PropSource = "source";
    private const string PropUserId = "userId";
    private const string PropText = "text";
    private const string PropId = "id";
    private const string ErrorMissingToken = "Invalid LINE configuration: missing ChannelAccessToken";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderLine;

    public bool SupportsPhoneAsRecipient => false;

    public LineMessagingProvider(HttpClient httpClient, ILogger<LineMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.ChannelAccessToken))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingToken);

        var payload = new
        {
            to = request.Recipient,
            messages = new[] { new { type = MessageTypeText, text = request.Content } }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, PushMessageUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonContentType)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.ChannelAccessToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LINE API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                var errorDetail = ExtractErrorMessage(responseBody);
                var errorMessage = string.IsNullOrWhiteSpace(errorDetail)
                    ? $"LINE API error: {response.StatusCode}"
                    : $"LINE API error: {response.StatusCode} - {errorDetail}";
                return new SendMessageResult(false, ErrorMessage: errorMessage);
            }

            var messageId = ExtractSentMessageId(responseBody);
            return new SendMessageResult(true, ExternalMessageId: messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.ChannelAccessToken))
            return false;

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BotInfoUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.ChannelAccessToken);

            var response = await _httpClient.SendAsync(httpRequest, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public WebhookValidationResult ValidateWebhook(WebhookValidationContext context)
    {
        var config = DeserializeConfig(context.ConfigJson);
        if (config == null || string.IsNullOrWhiteSpace(config.ChannelSecret))
        {
            _logger.LogWarning("LINE webhook validation failed: missing ChannelSecret in configuration");
            return new WebhookValidationResult(false);
        }

        var signature = context.GetHeader(SignatureHeader);
        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("LINE webhook validation failed: missing signature header");
            return new WebhookValidationResult(false);
        }

        var expectedSignature = ComputeSignature(config.ChannelSecret, context.Body);
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature));

        if (!isValid)
        {
            _logger.LogWarning("LINE webhook validation failed: signature mismatch");
            return new WebhookValidationResult(false);
        }

        return new WebhookValidationResult(true);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
            if (!TryGetArray(json, PropEvents, out var events))
                return null;

            foreach (var eventElement in events.EnumerateArray())
            {
                var message = ParseTextMessageEvent(eventElement);
                if (message != null)
                    return message;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IncomingMessage? ParseTextMessageEvent(JsonElement eventElement)
    {
        if (eventElement.ValueKind != JsonValueKind.Object
            || GetStringProperty(eventElement, PropType) != MessageEventType)
            return null;

        if (!eventElement.TryGetProperty(PropMessage, out var message)
            || message.ValueKind != JsonValueKind.Object
            || GetStringProperty(message, PropType) != MessageTypeText)
            return null;

        if (!eventElement.TryGetProperty(PropSource, out var source) || source.ValueKind != JsonValueKind.Object)
            return null;

        var userId = GetStringProperty(source, PropUserId);
        var text = GetStringProperty(message, PropText);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(text))
            return null;

        var messageId = GetStringProperty(message, PropId) ?? string.Empty;
        return new IncomingMessage(messageId, userId, userId, text);
    }

    private static string ComputeSignature(string channelSecret, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(channelSecret), Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }

    private static string? ExtractSentMessageId(string responseBody)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            if (TryGetArray(json, PropSentMessages, out var sentMessages)
                && sentMessages.GetArrayLength() > 0
                && sentMessages[0].ValueKind == JsonValueKind.Object)
            {
                return GetStringProperty(sentMessages[0], PropId);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractErrorMessage(string responseBody)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return json.ValueKind == JsonValueKind.Object
                ? GetStringProperty(json, PropMessage)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static LineConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<LineConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record LineConfig
    {
        public string ChannelAccessToken { get; init; } = string.Empty;
        public string ChannelSecret { get; init; } = string.Empty;
        public string WebhookUrl { get; init; } = string.Empty;
    }
}
