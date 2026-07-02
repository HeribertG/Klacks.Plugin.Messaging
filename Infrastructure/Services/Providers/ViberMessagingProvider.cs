// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Viber Bot/Channel API messaging provider adapter.
/// Sends messages via send_message, validates configuration via get_account_info,
/// registers the webhook via set_webhook, validates incoming webhooks with the
/// Viber HMAC-SHA256 content signature and parses message event payloads.
/// Viber responds with HTTP 200 even on errors; success is indicated by the
/// numeric status field in the response body (0 = ok).
/// </summary>
/// <param name="httpClient">HTTP client for Viber API requests</param>
/// <param name="logger">Logger instance</param>
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class ViberMessagingProvider : IMessagingProviderAdapter, IWebhookRegistrar
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ViberMessagingProvider> _logger;

    private const string SendMessageUrl = "https://chatapi.viber.com/pa/send_message";
    private const string GetAccountInfoUrl = "https://chatapi.viber.com/pa/get_account_info";
    private const string SetWebhookUrl = "https://chatapi.viber.com/pa/set_webhook";
    private const string AuthTokenHeader = "X-Viber-Auth-Token";
    private const string SignatureHeader = "X-Viber-Content-Signature";
    private const string JsonContentType = "application/json";
    private const string EmptyJsonBody = "{}";
    private const string DefaultSenderName = "Klacks";
    private const string MessageEvent = "message";
    private const string TextMessageType = "text";
    private const string StatusProperty = "status";
    private const string StatusMessageProperty = "status_message";
    private const string MessageTokenProperty = "message_token";
    private const string EventProperty = "event";
    private const string SenderProperty = "sender";
    private const string IdProperty = "id";
    private const string NameProperty = "name";
    private const string MessageProperty = "message";
    private const string TypeProperty = "type";
    private const string TextProperty = "text";
    private const int StatusOk = 0;
    private const string ErrorMissingAuthToken = "Invalid Viber configuration: missing AuthToken";
    private const string ErrorUnknown = "unknown";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderViber;

    public bool SupportsPhoneAsRecipient => false;

    public ViberMessagingProvider(HttpClient httpClient, ILogger<ViberMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AuthToken))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingAuthToken);

        var senderName = string.IsNullOrWhiteSpace(config.SenderName) ? DefaultSenderName : config.SenderName;
        var payload = new
        {
            receiver = request.Recipient,
            type = TextMessageType,
            text = request.Content,
            sender = new { name = senderName }
        };
        using var httpRequest = BuildRequest(SendMessageUrl, JsonSerializer.Serialize(payload), config.AuthToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Viber API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: $"Viber API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            if (!IsStatusOk(result))
            {
                var error = GetStringProperty(result, StatusMessageProperty) ?? ErrorUnknown;
                _logger.LogWarning("Viber send_message failed: {Error}", error);
                return new SendMessageResult(false, ErrorMessage: $"Viber API error: {error}");
            }

            return new SendMessageResult(true, ExternalMessageId: GetMessageToken(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Viber message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AuthToken))
            return false;

        try
        {
            using var httpRequest = BuildRequest(GetAccountInfoUrl, EmptyJsonBody, config.AuthToken);
            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return IsStatusOk(result);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RegisterWebhookAsync(string configJson, string webhookSecret, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AuthToken))
        {
            _logger.LogWarning("Viber webhook registration skipped: missing AuthToken in configuration");
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            _logger.LogWarning("Viber webhook registration skipped: missing WebhookUrl in configuration");
            return false;
        }

        var payload = new { url = config.WebhookUrl, event_types = new[] { MessageEvent }, send_name = true };
        using var httpRequest = BuildRequest(SetWebhookUrl, JsonSerializer.Serialize(payload), config.AuthToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Viber set_webhook failed: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return false;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            if (!IsStatusOk(result))
            {
                var error = GetStringProperty(result, StatusMessageProperty) ?? ErrorUnknown;
                _logger.LogWarning("Viber set_webhook failed: {Error}", error);
                return false;
            }

            _logger.LogInformation("Viber webhook registered at {WebhookUrl}", config.WebhookUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Viber webhook");
            return false;
        }
    }

    public WebhookValidationResult ValidateWebhook(WebhookValidationContext context)
    {
        var config = DeserializeConfig(context.ConfigJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AuthToken))
        {
            _logger.LogWarning("Viber webhook validation failed: missing AuthToken in configuration");
            return new WebhookValidationResult(false);
        }

        var signature = context.GetHeader(SignatureHeader);
        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Viber webhook validation failed: missing content signature header");
            return new WebhookValidationResult(false);
        }

        var expectedSignature = ComputeSignature(config.AuthToken, context.Body);
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedSignature));

        if (!isValid)
        {
            _logger.LogWarning("Viber webhook validation failed: signature mismatch");
            return new WebhookValidationResult(false);
        }

        return new WebhookValidationResult(true);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
            if (json.ValueKind != JsonValueKind.Object || GetStringProperty(json, EventProperty) != MessageEvent)
                return null;

            if (!json.TryGetProperty(MessageProperty, out var message) || message.ValueKind != JsonValueKind.Object)
                return null;

            if (GetStringProperty(message, TypeProperty) != TextMessageType)
                return null;

            if (!json.TryGetProperty(SenderProperty, out var sender) || sender.ValueKind != JsonValueKind.Object)
                return null;

            var senderId = GetStringProperty(sender, IdProperty);
            var text = GetStringProperty(message, TextProperty);
            if (string.IsNullOrWhiteSpace(senderId) || string.IsNullOrWhiteSpace(text))
                return null;

            var senderName = GetStringProperty(sender, NameProperty);
            var displayName = string.IsNullOrWhiteSpace(senderName) ? senderId : senderName;
            var messageId = GetMessageToken(json) ?? string.Empty;

            return new IncomingMessage(messageId, senderId, displayName, text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HttpRequestMessage BuildRequest(string url, string jsonBody, string authToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, JsonContentType)
        };
        request.Headers.Add(AuthTokenHeader, authToken);
        return request;
    }

    private static string ComputeSignature(string authToken, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(authToken), Encoding.UTF8.GetBytes(body));
        return Convert.ToHexStringLower(hash);
    }

    private static bool IsStatusOk(JsonElement result)
    {
        return result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty(StatusProperty, out var status)
            && status.ValueKind == JsonValueKind.Number
            && status.GetInt32() == StatusOk;
    }

    private static string? GetMessageToken(JsonElement element)
    {
        return element.TryGetProperty(MessageTokenProperty, out var token)
            && token.ValueKind is JsonValueKind.Number or JsonValueKind.String
            ? token.ToString()
            : null;
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static ViberConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ViberConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record ViberConfig
    {
        public string AuthToken { get; init; } = string.Empty;
        public string SenderName { get; init; } = string.Empty;
        public string WebhookUrl { get; init; } = string.Empty;
    }
}
