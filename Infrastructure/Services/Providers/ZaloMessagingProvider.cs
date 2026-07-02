// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Zalo messaging provider adapter (dominant messenger in Vietnam) using the
/// Zalo Official Account Open API v3 customer service message endpoint.
/// Outgoing only: webhook validation and payload parsing are not supported because
/// Zalo webhooks require a separate app event registration with its own signature scheme.
/// The OA access token expires after roughly 25 hours and must be refreshed externally.
/// </summary>
/// <param name="httpClient">HTTP client for Zalo API requests</param>
/// <param name="logger">Logger instance</param>
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class ZaloMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZaloMessagingProvider> _logger;

    private const string SendMessageUrl = "https://openapi.zalo.me/v3.0/oa/message/cs";
    private const string GetOaInfoUrl = "https://openapi.zalo.me/v2.0/oa/getoa";
    private const string AccessTokenHeader = "access_token";
    private const string JsonContentType = "application/json";
    private const string ErrorProperty = "error";
    private const string MessageProperty = "message";
    private const string DataProperty = "data";
    private const string MessageIdProperty = "message_id";
    private const long SuccessErrorCode = 0;
    private const string ErrorMissingAccessToken = "Invalid Zalo configuration: missing AccessToken";
    private const string ErrorMissingRecipient = "No Zalo recipient specified: recipient user id is empty";
    private const string ErrorUnknown = "unknown";
    private const string ErrorMissingErrorCode = "Zalo API error: unexpected response without error code";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderZalo;

    public bool SupportsPhoneAsRecipient => false;

    public ZaloMessagingProvider(HttpClient httpClient, ILogger<ZaloMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingAccessToken);

        if (string.IsNullOrWhiteSpace(request.Recipient))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingRecipient);

        var payload = new
        {
            recipient = new { user_id = request.Recipient },
            message = new { text = request.Content }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SendMessageUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonContentType)
        };
        httpRequest.Headers.TryAddWithoutValidation(AccessTokenHeader, config.AccessToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Zalo API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: $"Zalo API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            var errorCode = GetErrorCode(result);
            if (errorCode == null)
            {
                _logger.LogWarning("Zalo API returned a response without error code: {Body}", responseBody);
                return new SendMessageResult(false, ErrorMessage: ErrorMissingErrorCode);
            }

            if (errorCode != SuccessErrorCode)
            {
                var errorText = GetStringProperty(result, MessageProperty) ?? ErrorUnknown;
                _logger.LogWarning("Zalo message send failed: {ErrorCode} - {ErrorText}", errorCode, errorText);
                return new SendMessageResult(false, ErrorMessage: $"Zalo API error {errorCode}: {errorText}");
            }

            var messageId = GetDataMessageId(result);
            return new SendMessageResult(true, ExternalMessageId: messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Zalo message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken))
            return false;

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, GetOaInfoUrl);
            httpRequest.Headers.TryAddWithoutValidation(AccessTokenHeader, config.AccessToken);

            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return GetErrorCode(result) == SuccessErrorCode;
        }
        catch
        {
            return false;
        }
    }

    public WebhookValidationResult ValidateWebhook(WebhookValidationContext context)
    {
        return new WebhookValidationResult(false);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        return null;
    }

    private static long? GetErrorCode(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(ErrorProperty, out var error)
            && error.ValueKind == JsonValueKind.Number
            ? error.GetInt64()
            : null;
    }

    private static string? GetDataMessageId(JsonElement element)
    {
        return element.TryGetProperty(DataProperty, out var data) && data.ValueKind == JsonValueKind.Object
            ? GetStringProperty(data, MessageIdProperty)
            : null;
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static ZaloConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ZaloConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record ZaloConfig
    {
        public string AccessToken { get; init; } = string.Empty;
        public string OaId { get; init; } = string.Empty;
    }
}
