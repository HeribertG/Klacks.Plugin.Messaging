// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// SMS messaging provider adapter for Twilio-compatible SMS gateway REST APIs.
/// Sends outgoing text messages via the Messages endpoint using HTTP basic authentication;
/// receiving messages via webhook is not supported in this stage (outgoing only).
/// </summary>
/// <param name="httpClient">HTTP client for SMS gateway API requests</param>
/// <param name="logger">Logger instance</param>
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class SmsMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SmsMessagingProvider> _logger;

    private const string DefaultGatewayUrl = "https://api.twilio.com";
    private const string ApiVersionSegment = "2010-04-01";
    private const string AccountsSegment = "Accounts";
    private const string MessagesResourceSegment = "Messages.json";
    private const string JsonResourceSuffix = ".json";
    private const string BasicAuthScheme = "Basic";
    private const string ToFieldName = "To";
    private const string FromFieldName = "From";
    private const string BodyFieldName = "Body";
    private const string SidPropertyName = "sid";
    private const string ErrorMessagePropertyName = "message";
    private const string GatewayErrorPrefix = "SMS gateway error";
    private const string MissingConfigErrorMessage = "Invalid SMS configuration: AccountSid, AuthToken and SenderNumber are required";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderSms;

    public bool SupportsPhoneAsRecipient => true;

    public SmsMessagingProvider(HttpClient httpClient, ILogger<SmsMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = ParseConfig(configJson);
        if (config == null || !HasRequiredFields(config))
            return new SendMessageResult(false, ErrorMessage: MissingConfigErrorMessage);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildMessagesUrl(config))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [ToFieldName] = request.Recipient,
                [FromFieldName] = config.SenderNumber,
                [BodyFieldName] = request.Content
            })
        };
        httpRequest.Headers.Authorization = BuildBasicAuthHeader(config);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SMS gateway error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: BuildErrorMessage(response.StatusCode, responseBody));
            }

            return new SendMessageResult(true, ExternalMessageId: ExtractJsonStringProperty(responseBody, SidPropertyName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = ParseConfig(configJson);
        if (config == null || !HasRequiredFields(config))
            return false;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildAccountUrl(config));
        httpRequest.Headers.Authorization = BuildBasicAuthHeader(config);

        try
        {
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
        return new WebhookValidationResult(false);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        return null;
    }

    private static SmsConfig? ParseConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<SmsConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasRequiredFields(SmsConfig config)
    {
        return !string.IsNullOrWhiteSpace(config.AccountSid)
            && !string.IsNullOrWhiteSpace(config.AuthToken)
            && !string.IsNullOrWhiteSpace(config.SenderNumber);
    }

    private static string BuildBaseUrl(SmsConfig config)
    {
        return string.IsNullOrWhiteSpace(config.GatewayUrl)
            ? DefaultGatewayUrl
            : config.GatewayUrl.TrimEnd('/');
    }

    private static string BuildMessagesUrl(SmsConfig config)
    {
        return $"{BuildBaseUrl(config)}/{ApiVersionSegment}/{AccountsSegment}/{config.AccountSid}/{MessagesResourceSegment}";
    }

    private static string BuildAccountUrl(SmsConfig config)
    {
        return $"{BuildBaseUrl(config)}/{ApiVersionSegment}/{AccountsSegment}/{config.AccountSid}{JsonResourceSuffix}";
    }

    private static AuthenticationHeaderValue BuildBasicAuthHeader(SmsConfig config)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.AccountSid}:{config.AuthToken}"));
        return new AuthenticationHeaderValue(BasicAuthScheme, credentials);
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string responseBody)
    {
        var detail = ExtractJsonStringProperty(responseBody, ErrorMessagePropertyName);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{GatewayErrorPrefix}: {statusCode}"
            : $"{GatewayErrorPrefix}: {statusCode} - {detail}";
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

    private record SmsConfig
    {
        public string AccountSid { get; init; } = string.Empty;
        public string AuthToken { get; init; } = string.Empty;
        public string SenderNumber { get; init; } = string.Empty;
        public string GatewayUrl { get; init; } = string.Empty;
    }
}
