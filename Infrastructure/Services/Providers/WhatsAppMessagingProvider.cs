// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// WhatsApp Cloud API messaging provider adapter (Meta Business Platform).
/// Sends text messages via the Meta Graph API, validates webhook signatures (X-Hub-Signature-256),
/// parses incoming Meta webhook payloads and verifies subscription handshakes (hub.verify_token).
/// </summary>
/// <param name="httpClient">HTTP client for Meta Graph API requests</param>
/// <param name="logger">Logger instance</param>
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class WhatsAppMessagingProvider : IMessagingProviderAdapter, IWebhookSubscriptionVerifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppMessagingProvider> _logger;

    private const string GraphApiBaseUrl = "https://graph.facebook.com";
    private const string GraphApiVersion = "v21.0";
    private const string MessagesEndpoint = "messages";
    private const string ConfigValidationQuery = "?fields=id";
    private const string BearerScheme = "Bearer";
    private const string JsonMediaType = "application/json";
    private const string SignatureHeader = "X-Hub-Signature-256";
    private const string SignaturePrefix = "sha256=";
    private const string MessagingProduct = "whatsapp";
    private const string RecipientTypeIndividual = "individual";
    private const string MessageTypeText = "text";

    private const string PropEntry = "entry";
    private const string PropChanges = "changes";
    private const string PropValue = "value";
    private const string PropMessages = "messages";
    private const string PropContacts = "contacts";
    private const string PropProfile = "profile";
    private const string PropName = "name";
    private const string PropWaId = "wa_id";
    private const string PropFrom = "from";
    private const string PropId = "id";
    private const string PropType = "type";
    private const string PropText = "text";
    private const string PropBody = "body";
    private const string PropError = "error";
    private const string PropMessage = "message";

    private const string MissingConfigError = "Invalid WhatsApp configuration: missing AccessToken or PhoneNumberId";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderWhatsApp;

    public bool SupportsPhoneAsRecipient => true;

    public WhatsAppMessagingProvider(HttpClient httpClient, ILogger<WhatsAppMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken) || string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return new SendMessageResult(false, ErrorMessage: MissingConfigError);

        var url = $"{GraphApiBaseUrl}/{GraphApiVersion}/{config.PhoneNumberId}/{MessagesEndpoint}";
        var payload = new
        {
            messaging_product = MessagingProduct,
            recipient_type = RecipientTypeIndividual,
            to = request.Recipient,
            type = MessageTypeText,
            text = new { body = request.Content }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonMediaType)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.AccessToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WhatsApp API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                var errorDetail = ExtractErrorMessage(responseBody);
                var errorMessage = string.IsNullOrWhiteSpace(errorDetail)
                    ? $"WhatsApp API error: {response.StatusCode}"
                    : $"WhatsApp API error: {response.StatusCode} - {errorDetail}";
                return new SendMessageResult(false, ErrorMessage: errorMessage);
            }

            var messageId = ExtractMessageId(responseBody);
            return new SendMessageResult(true, ExternalMessageId: messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken) || string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return false;

        try
        {
            var url = $"{GraphApiBaseUrl}/{GraphApiVersion}/{config.PhoneNumberId}{ConfigValidationQuery}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.AccessToken);
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
        if (config == null || string.IsNullOrWhiteSpace(config.AppSecret))
        {
            _logger.LogWarning("WhatsApp webhook validation failed: AppSecret is not configured");
            return new WebhookValidationResult(false);
        }

        var signatureHeader = context.GetHeader(SignatureHeader);
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return new WebhookValidationResult(false);

        var receivedSignature = signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase)
            ? signatureHeader[SignaturePrefix.Length..]
            : signatureHeader;

        var computedHash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(config.AppSecret),
            Encoding.UTF8.GetBytes(context.Body));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(receivedSignature.ToLowerInvariant()));

        return new WebhookValidationResult(isValid);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
            if (!TryGetArray(json, PropEntry, out var entries))
                return null;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!TryGetArray(entry, PropChanges, out var changes))
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (change.ValueKind != JsonValueKind.Object
                        || !change.TryGetProperty(PropValue, out var value)
                        || value.ValueKind != JsonValueKind.Object)
                        continue;

                    var message = ParseTextMessage(value);
                    if (message != null)
                        return message;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool VerifySubscription(string configJson, string verifyToken)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.VerifyToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(verifyToken),
            Encoding.UTF8.GetBytes(config.VerifyToken));
    }

    private static IncomingMessage? ParseTextMessage(JsonElement value)
    {
        if (!TryGetArray(value, PropMessages, out var messages))
            return null;

        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty(PropType, out var type)
                || type.GetString() != MessageTypeText)
                continue;

            var sender = GetStringProperty(message, PropFrom);
            var messageId = GetStringProperty(message, PropId);
            var content = message.TryGetProperty(PropText, out var text) && text.ValueKind == JsonValueKind.Object
                ? GetStringProperty(text, PropBody)
                : null;

            if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(content))
                continue;

            var displayName = ResolveSenderDisplayName(value, sender);
            return new IncomingMessage(messageId, sender, displayName, content);
        }

        return null;
    }

    private static string ResolveSenderDisplayName(JsonElement value, string sender)
    {
        if (!TryGetArray(value, PropContacts, out var contacts))
            return sender;

        JsonElement? firstContact = null;
        foreach (var contact in contacts.EnumerateArray())
        {
            if (contact.ValueKind != JsonValueKind.Object)
                continue;

            firstContact ??= contact;
            if (GetStringProperty(contact, PropWaId) == sender)
                return GetProfileName(contact) ?? sender;
        }

        return firstContact.HasValue ? GetProfileName(firstContact.Value) ?? sender : sender;
    }

    private static string? GetProfileName(JsonElement contact)
    {
        return contact.TryGetProperty(PropProfile, out var profile) && profile.ValueKind == JsonValueKind.Object
            ? GetStringProperty(profile, PropName)
            : null;
    }

    private static string? ExtractErrorMessage(string responseBody)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            if (json.ValueKind == JsonValueKind.Object
                && json.TryGetProperty(PropError, out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                return GetStringProperty(error, PropMessage);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractMessageId(string responseBody)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            if (TryGetArray(json, PropMessages, out var messages) && messages.GetArrayLength() > 0
                && messages[0].ValueKind == JsonValueKind.Object)
            {
                return GetStringProperty(messages[0], PropId);
            }

            return null;
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

    private static WhatsAppConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<WhatsAppConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record WhatsAppConfig
    {
        public string AccessToken { get; init; } = string.Empty;
        public string PhoneNumberId { get; init; } = string.Empty;
        public string BusinessAccountId { get; init; } = string.Empty;
        public string AppSecret { get; init; } = string.Empty;
        public string VerifyToken { get; init; } = string.Empty;
        public string WebhookUrl { get; init; } = string.Empty;
    }
}
