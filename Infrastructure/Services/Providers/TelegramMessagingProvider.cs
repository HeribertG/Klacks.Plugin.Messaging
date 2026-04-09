// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Telegram Bot API messaging provider adapter.
/// Uses the Telegram Bot HTTP API for sending and receiving messages.
/// </summary>
/// <param name="httpClient">HTTP client for Telegram API requests</param>
/// <param name="logger">Logger instance</param>
using System.Security.Cryptography;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public class TelegramMessagingProvider : IMessagingProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TelegramMessagingProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ProviderType => MessagingConstants.ProviderTelegram;

    public bool SupportsPhoneAsRecipient => false;

    public TelegramMessagingProvider(HttpClient httpClient, IMemoryCache cache, ILogger<TelegramMessagingProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetBotUsernameAsync(string configJson, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<TelegramConfig>(configJson, JsonOptions);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return null;

        var cacheKey = $"telegram_bot_username_{config.BotToken.GetHashCode()}";
        if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        try
        {
            var url = $"https://api.telegram.org/bot{config.BotToken}/getMe";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            if (result.TryGetProperty("result", out var r) && r.TryGetProperty("username", out var username))
            {
                var value = username.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _cache.Set(cacheKey, value, TimeSpan.FromMinutes(MessagingConstants.BotUsernameCacheMinutes));
                    return value;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Telegram bot username");
            return null;
        }
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, string configJson, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<TelegramConfig>(configJson, JsonOptions);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return new SendMessageResult(false, ErrorMessage: "Invalid Telegram configuration: missing BotToken");

        var url = $"https://api.telegram.org/bot{config.BotToken}/sendMessage";
        object chatId = long.TryParse(request.Recipient, out var numericId)
            ? numericId
            : request.Recipient;
        var payload = new { chat_id = chatId, text = request.Content };
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SendMessageResult(false, ErrorMessage: $"Telegram API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var messageId = result.TryGetProperty("result", out var r) && r.TryGetProperty("message_id", out var mid)
                ? mid.ToString()
                : null;

            return new SendMessageResult(true, ExternalMessageId: messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message");
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<bool> ValidateConfigAsync(string configJson, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<TelegramConfig>(configJson, JsonOptions);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return false;

        try
        {
            var url = $"https://api.telegram.org/bot{config.BotToken}/getMe";
            var response = await _httpClient.GetAsync(url, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public WebhookValidationResult ValidateWebhook(string body, string signature, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(webhookSecret))
            return new WebhookValidationResult(true);

        return new WebhookValidationResult(
            CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(signature),
                System.Text.Encoding.UTF8.GetBytes(webhookSecret)));
    }

    public IncomingMessage? ParseWebhookPayload(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);

            if (!json.TryGetProperty("message", out var message))
                return null;

            var messageId = message.TryGetProperty("message_id", out var mid) ? mid.ToString() : "";
            var text = message.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            var from = message.TryGetProperty("from", out var f) ? f : default;
            var senderId = from.ValueKind != JsonValueKind.Undefined && from.TryGetProperty("id", out var sid) ? sid.ToString() : "";
            var senderName = from.ValueKind != JsonValueKind.Undefined && from.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(text))
                return null;

            return new IncomingMessage(messageId, senderId, senderName, text);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private record TelegramConfig
    {
        public string BotToken { get; init; } = string.Empty;
        public string WebhookUrl { get; init; } = string.Empty;
    }
}
