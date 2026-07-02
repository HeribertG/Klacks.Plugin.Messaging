// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// KakaoTalk messaging provider adapter using the Kakao Talk messaging API
/// ("Send message to friends" with the default text template).
/// The recipient is a Kakao friend UUID obtained via the Kakao friends API; the access token
/// is short-lived (~6 hours) and must be refreshed externally; sending requires the
/// talk_message scope and an established friend relationship between the Kakao account
/// linked to the access token and the recipient. Outgoing only — Kakao offers no message
/// webhook for regular apps, so webhook validation and payload parsing are not supported.
/// </summary>
/// <param name="httpClient">HTTP client for Kakao API requests</param>
/// <param name="logger">Logger instance</param>
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class KakaoTalkMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KakaoTalkMessagingProvider> _logger;

    private const string SendMessageUrl = "https://kapi.kakao.com/v1/api/talk/friends/message/default/send";
    private const string TokenInfoUrl = "https://kapi.kakao.com/v1/user/access_token_info";
    private const string BearerScheme = "Bearer";
    private const string ReceiverUuidsFieldName = "receiver_uuids";
    private const string TemplateObjectFieldName = "template_object";
    private const string TemplateObjectTypeText = "text";
    private const string PropSuccessfulReceiverUuids = "successful_receiver_uuids";
    private const string PropFailureInfo = "failure_info";
    private const string PropErrorMessage = "msg";
    private const string PropErrorCode = "code";
    private const string KakaoApiErrorPrefix = "KakaoTalk API error";
    private const string SendFailurePrefix = "KakaoTalk send failed";
    private const string MissingConfigError = "Invalid KakaoTalk configuration: AccessToken is required";
    private const string FailureInfoReportedError =
        $"{SendFailurePrefix}: the Kakao API reported {PropFailureInfo} for the message";
    private const string DeliveryNotConfirmedError =
        $"{SendFailurePrefix}: the recipient UUID was not confirmed in {PropSuccessfulReceiverUuids}";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderKakaoTalk;

    public bool SupportsPhoneAsRecipient => false;

    public KakaoTalkMessagingProvider(HttpClient httpClient, ILogger<KakaoTalkMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken))
            return new SendMessageResult(false, ErrorMessage: MissingConfigError);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SendMessageUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [ReceiverUuidsFieldName] = JsonSerializer.Serialize(new[] { request.Recipient }),
                [TemplateObjectFieldName] = BuildTemplateObject(request.Content)
            })
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.AccessToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("KakaoTalk API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: BuildHttpErrorMessage(response.StatusCode, responseBody));
            }

            return EvaluateSendResponse(request.Recipient, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send KakaoTalk message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken))
            return false;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, TokenInfoUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.AccessToken);

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

    private static string BuildTemplateObject(string content)
    {
        return JsonSerializer.Serialize(new
        {
            object_type = TemplateObjectTypeText,
            text = content,
            link = new { }
        });
    }

    private static SendMessageResult EvaluateSendResponse(string recipient, string responseBody)
    {
        JsonElement json;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return new SendMessageResult(false, ErrorMessage: DeliveryNotConfirmedError);
        }

        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(PropFailureInfo, out var failureInfo))
        {
            var detail = ExtractFailureDetail(failureInfo);
            var errorMessage = string.IsNullOrWhiteSpace(detail)
                ? FailureInfoReportedError
                : $"{SendFailurePrefix}: {detail}";
            return new SendMessageResult(false, ErrorMessage: errorMessage);
        }

        return IsRecipientSuccessful(json, recipient)
            ? new SendMessageResult(true)
            : new SendMessageResult(false, ErrorMessage: DeliveryNotConfirmedError);
    }

    private static bool IsRecipientSuccessful(JsonElement json, string recipient)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty(PropSuccessfulReceiverUuids, out var uuids)
            || uuids.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var uuid in uuids.EnumerateArray())
        {
            if (uuid.ValueKind == JsonValueKind.String && uuid.GetString() == recipient)
                return true;
        }

        return false;
    }

    private static string? ExtractFailureDetail(JsonElement failureInfo)
    {
        var element = failureInfo.ValueKind == JsonValueKind.Array && failureInfo.GetArrayLength() > 0
            ? failureInfo[0]
            : failureInfo;

        return element.ValueKind == JsonValueKind.Object
            ? FormatMessageAndCode(element)
            : null;
    }

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseBody)
    {
        var detail = ExtractHttpErrorDetail(responseBody);
        return string.IsNullOrWhiteSpace(detail)
            ? $"{KakaoApiErrorPrefix}: {statusCode}"
            : $"{KakaoApiErrorPrefix}: {statusCode} - {detail}";
    }

    private static string? ExtractHttpErrorDetail(string responseBody)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            return json.ValueKind == JsonValueKind.Object ? FormatMessageAndCode(json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FormatMessageAndCode(JsonElement element)
    {
        var message = GetStringProperty(element, PropErrorMessage);
        var code = GetIntProperty(element, PropErrorCode);

        if (message == null && code == null)
            return null;

        if (message != null && code != null)
            return $"{message} (code {code})";

        return message ?? $"code {code}";
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static KakaoTalkConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<KakaoTalkConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private record KakaoTalkConfig
    {
        public string AccessToken { get; init; } = string.Empty;
    }
}
