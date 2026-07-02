// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Threema messaging provider adapter for the official Threema Gateway in basic mode
/// (server-side encryption, outgoing text messages via the send_simple endpoint).
/// Receiving messages is not supported because incoming messages exist only in
/// end-to-end mode with client-side NaCl cryptography, which is out of scope here.
/// </summary>
/// <param name="httpClient">HTTP client for Threema Gateway API requests</param>
/// <param name="logger">Logger instance</param>
using System.Net;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class ThreemaMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThreemaMessagingProvider> _logger;

    private const string SendSimpleUrl = "https://msgapi.threema.ch/send_simple";
    private const string CreditsUrl = "https://msgapi.threema.ch/credits";
    private const string FromFieldName = "from";
    private const string ToFieldName = "to";
    private const string TextFieldName = "text";
    private const string SecretFieldName = "secret";
    private const string GatewayErrorPrefix = "Threema gateway error";
    private const string InvalidRecipientErrorMessage = $"{GatewayErrorPrefix}: invalid recipient or identity";
    private const string InvalidCredentialsErrorMessage = $"{GatewayErrorPrefix}: invalid gateway credentials";
    private const string NoCreditsErrorMessage = $"{GatewayErrorPrefix}: no gateway credits left";
    private const string RecipientNotFoundErrorMessage = $"{GatewayErrorPrefix}: recipient not found";
    private const string MissingConfigErrorMessage = "Invalid Threema configuration: GatewayId and ApiSecret are required";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderThreema;

    public bool SupportsPhoneAsRecipient => false;

    public ThreemaMessagingProvider(HttpClient httpClient, ILogger<ThreemaMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || !HasRequiredFields(config))
            return new SendMessageResult(false, ErrorMessage: MissingConfigErrorMessage);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SendSimpleUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [FromFieldName] = config.GatewayId,
                [ToFieldName] = request.Recipient,
                [TextFieldName] = request.Content,
                [SecretFieldName] = config.ApiSecret
            })
        };

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Threema gateway error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: MapErrorMessage(response.StatusCode));
            }

            var messageId = responseBody.Trim();
            return new SendMessageResult(true, ExternalMessageId: messageId.Length > 0 ? messageId : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Threema message");
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
            var response = await _httpClient.GetAsync(BuildCreditsUrl(config), ct);
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

    private static ThreemaConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ThreemaConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasRequiredFields(ThreemaConfig config)
    {
        return !string.IsNullOrWhiteSpace(config.GatewayId)
            && !string.IsNullOrWhiteSpace(config.ApiSecret);
    }

    private static string BuildCreditsUrl(ThreemaConfig config)
    {
        return $"{CreditsUrl}?{FromFieldName}={Uri.EscapeDataString(config.GatewayId)}&{SecretFieldName}={Uri.EscapeDataString(config.ApiSecret)}";
    }

    private static string MapErrorMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => InvalidRecipientErrorMessage,
            HttpStatusCode.Unauthorized => InvalidCredentialsErrorMessage,
            HttpStatusCode.PaymentRequired => NoCreditsErrorMessage,
            HttpStatusCode.NotFound => RecipientNotFoundErrorMessage,
            _ => $"{GatewayErrorPrefix}: {statusCode}"
        };
    }

    private record ThreemaConfig
    {
        public string GatewayId { get; init; } = string.Empty;
        public string ApiSecret { get; init; } = string.Empty;
    }
}
