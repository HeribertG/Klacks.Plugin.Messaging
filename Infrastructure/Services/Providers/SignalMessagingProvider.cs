// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Signal messaging provider adapter for a self-hosted signal-cli-rest-api container.
/// Sends outgoing text messages via the /v2/send endpoint of the local REST API;
/// receiving messages via webhook is not supported (outgoing only).
/// </summary>
/// <param name="httpClient">HTTP client for signal-cli-rest-api requests</param>
/// <param name="logger">Logger instance</param>
using System.Net;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class SignalMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SignalMessagingProvider> _logger;

    private const string SendEndpointPath = "v2/send";
    private const string AccountsEndpointPath = "v1/accounts";
    private const string JsonMediaType = "application/json";
    private const string TimestampPropertyName = "timestamp";
    private const string ErrorPropertyName = "error";
    private const string ApiErrorPrefix = "Signal API error";
    private const string MissingConfigErrorMessage = "Invalid Signal configuration: SignalNumber and ApiUrl are required";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderSignal;

    public bool SupportsPhoneAsRecipient => true;

    public SignalMessagingProvider(HttpClient httpClient, ILogger<SignalMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || !HasRequiredFields(config))
            return new SendMessageResult(false, ErrorMessage: MissingConfigErrorMessage);

        var payload = new
        {
            message = request.Content,
            number = config.SignalNumber,
            recipients = new[] { request.Recipient }
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonMediaType);

        try
        {
            var response = await _httpClient.PostAsync(BuildSendUrl(config), content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Signal API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: BuildErrorMessage(response.StatusCode, responseBody));
            }

            return new SendMessageResult(true, ExternalMessageId: ExtractTimestamp(responseBody));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Signal message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || !HasRequiredFields(config))
            return false;

        try
        {
            var response = await _httpClient.GetAsync(BuildAccountsUrl(config), ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return AccountsListContainsNumber(responseBody, config.SignalNumber);
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

    private static SignalConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<SignalConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasRequiredFields(SignalConfig config)
    {
        return !string.IsNullOrWhiteSpace(config.SignalNumber)
            && !string.IsNullOrWhiteSpace(config.ApiUrl);
    }

    private static string BuildBaseUrl(SignalConfig config)
    {
        return config.ApiUrl.TrimEnd('/');
    }

    private static string BuildSendUrl(SignalConfig config)
    {
        return $"{BuildBaseUrl(config)}/{SendEndpointPath}";
    }

    private static string BuildAccountsUrl(SignalConfig config)
    {
        return $"{BuildBaseUrl(config)}/{AccountsEndpointPath}";
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string responseBody)
    {
        var detail = ExtractJsonStringProperty(responseBody, ErrorPropertyName);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{ApiErrorPrefix}: {statusCode}"
            : $"{ApiErrorPrefix}: {statusCode} - {detail}";
    }

    private static string? ExtractTimestamp(string responseBody)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(TimestampPropertyName, out var timestamp)
                && timestamp.ValueKind is JsonValueKind.Number or JsonValueKind.String
                ? timestamp.ToString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonStringProperty(string json, string propertyName)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool AccountsListContainsNumber(string responseBody, string signalNumber)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return element.ValueKind == JsonValueKind.Array
                && element.EnumerateArray().Any(entry =>
                    entry.ValueKind == JsonValueKind.String
                    && entry.GetString() == signalNumber);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private record SignalConfig
    {
        public string SignalNumber { get; init; } = string.Empty;
        public string ApiUrl { get; init; } = string.Empty;
    }
}
