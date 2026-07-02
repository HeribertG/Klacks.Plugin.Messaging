// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// WeChat Official Account messaging provider adapter (outgoing only).
/// Sends customer service text messages via the WeChat Official Account API using an
/// access token (client_credential grant) that is cached in memory until shortly before expiry.
/// Note: WeChat customer service messages require a user interaction with the Official
/// Account within the last 48 hours, otherwise the API rejects the send.
/// Inbound webhooks are intentionally out of scope because WeChat uses an XML payload
/// format with its own echo/AES verification scheme; webhook validation therefore always
/// returns invalid and payload parsing always returns null.
/// </summary>
/// <param name="httpClient">HTTP client for WeChat API requests</param>
/// <param name="cache">Memory cache for access tokens</param>
/// <param name="logger">Logger instance</param>
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class WeChatMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeChatMessagingProvider> _logger;

    private const string TokenUrlTemplate = "https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={0}&secret={1}";
    private const string SendMessageUrlTemplate = "https://api.weixin.qq.com/cgi-bin/message/custom/send?access_token={0}";
    private const string JsonContentType = "application/json";
    private const string AccessTokenProperty = "access_token";
    private const string ExpiresInProperty = "expires_in";
    private const string ErrorCodeProperty = "errcode";
    private const string ErrorMessageProperty = "errmsg";
    private const string TextMessageType = "text";
    private const string AccessTokenCacheKeyPrefix = "wechat_access_token_";
    private const int TokenExpirySafetyBufferSeconds = 300;
    private const int MinimumTokenCacheSeconds = 60;
    private const int DefaultTokenExpirySeconds = 7200;
    private const int SuccessErrorCode = 0;
    private const int ErrorCodeInvalidCredential = 40001;
    private const int ErrorCodeAccessTokenExpired = 42001;
    private const string ErrorMissingCredentials = "Invalid WeChat configuration: missing AppId or AppSecret";
    private const string ErrorTokenUnavailable = "Failed to obtain WeChat access token";
    private const string ErrorUnknown = "unknown";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderWeChat;

    public bool SupportsPhoneAsRecipient => false;

    public WeChatMessagingProvider(HttpClient httpClient, IMemoryCache cache, ILogger<WeChatMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AppId) || string.IsNullOrWhiteSpace(config.AppSecret))
            return new SendMessageResult(false, ErrorMessage: ErrorMissingCredentials);

        var accessToken = await GetAccessTokenAsync(config, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
            return new SendMessageResult(false, ErrorMessage: ErrorTokenUnavailable);

        var url = string.Format(SendMessageUrlTemplate, Uri.EscapeDataString(accessToken));
        var payload = new { touser = request.Recipient, msgtype = TextMessageType, text = new { content = request.Content } };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, JsonContentType);

        try
        {
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WeChat API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: $"WeChat API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            var errorCode = GetErrorCode(result);
            if (errorCode != SuccessErrorCode)
            {
                if (errorCode == ErrorCodeInvalidCredential || errorCode == ErrorCodeAccessTokenExpired)
                {
                    _cache.Remove(BuildAccessTokenCacheKey(config.AppId));
                }

                var errorMessage = GetStringProperty(result, ErrorMessageProperty) ?? ErrorUnknown;
                _logger.LogWarning("WeChat custom message send failed: {ErrorCode} - {ErrorMessage}", errorCode, errorMessage);
                return new SendMessageResult(false, ErrorMessage: $"WeChat API error {errorCode}: {errorMessage}");
            }

            return new SendMessageResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WeChat message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AppId) || string.IsNullOrWhiteSpace(config.AppSecret))
            return false;

        var freshToken = await FetchAccessTokenAsync(config, ct);
        return freshToken != null;
    }

    public WebhookValidationResult ValidateWebhook(WebhookValidationContext context)
    {
        return new WebhookValidationResult(false);
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        return null;
    }

    private async Task<string?> GetAccessTokenAsync(WeChatConfig config, CancellationToken ct)
    {
        var cacheKey = BuildAccessTokenCacheKey(config.AppId);
        if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        var freshToken = await FetchAccessTokenAsync(config, ct);
        if (freshToken == null)
            return null;

        var cacheSeconds = Math.Max(freshToken.ExpiresIn - TokenExpirySafetyBufferSeconds, MinimumTokenCacheSeconds);
        _cache.Set(cacheKey, freshToken.Token, TimeSpan.FromSeconds(cacheSeconds));
        return freshToken.Token;
    }

    private async Task<AccessTokenResponse?> FetchAccessTokenAsync(WeChatConfig config, CancellationToken ct)
    {
        try
        {
            var url = string.Format(TokenUrlTemplate, Uri.EscapeDataString(config.AppId), Uri.EscapeDataString(config.AppSecret));
            var response = await _httpClient.GetAsync(url, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WeChat token endpoint error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return null;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);
            var accessToken = GetStringProperty(result, AccessTokenProperty);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                var errorCode = GetErrorCode(result);
                var errorMessage = GetStringProperty(result, ErrorMessageProperty) ?? ErrorUnknown;
                _logger.LogWarning("WeChat access token request failed: {ErrorCode} - {ErrorMessage}", errorCode, errorMessage);
                return null;
            }

            var expiresIn = result.TryGetProperty(ExpiresInProperty, out var expires) && expires.ValueKind == JsonValueKind.Number
                ? expires.GetInt32()
                : DefaultTokenExpirySeconds;

            return new AccessTokenResponse(accessToken, expiresIn);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch WeChat access token");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse WeChat access token response");
            return null;
        }
    }

    private static string BuildAccessTokenCacheKey(string appId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(appId));
        return $"{AccessTokenCacheKeyPrefix}{Convert.ToHexString(hashBytes)}";
    }

    private static int GetErrorCode(JsonElement element)
    {
        return element.TryGetProperty(ErrorCodeProperty, out var code) && code.ValueKind == JsonValueKind.Number
            ? code.GetInt32()
            : SuccessErrorCode;
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static WeChatConfig? DeserializeConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<WeChatConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record AccessTokenResponse(string Token, int ExpiresIn);

    private record WeChatConfig
    {
        public string AppId { get; init; } = string.Empty;
        public string AppSecret { get; init; } = string.Empty;
    }
}
