// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Credential diagnosis and webhook inspection for the Telegram Bot API, kept as a partial class
/// extension so the main provider file stays focused on the messaging adapter contract.
/// </summary>
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class TelegramMessagingProvider : ICredentialDiagnoser, ITelegramWebhookInspector
{
    private const string GetMeMethod = "getMe";
    private const string GetWebhookInfoMethod = "getWebhookInfo";
    private const string DescriptionProperty = "description";
    private const string ResultProperty = "result";
    private const string UsernameProperty = "username";
    private const string UrlProperty = "url";
    private const string PendingUpdateCountProperty = "pending_update_count";
    private const string LastErrorMessageProperty = "last_error_message";
    private const string LastErrorDateProperty = "last_error_date";

    public async Task<CredentialDiagnosis> DiagnoseCredentialsAsync(string configJson, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<TelegramConfig>(configJson, JsonOptions);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return new CredentialDiagnosis(false, CredentialReasonCodes.MissingToken);

        try
        {
            var response = await _httpClient.GetAsync(BuildMethodUrl(config.BotToken, GetMeMethod), ct);
            if (!response.IsSuccessStatusCode && !VendorCredentialStatus.IsTelegramRejection(response.StatusCode))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);

            var body = await response.Content.ReadAsStringAsync(ct);
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(ReadString(json, DescriptionProperty)));

            var username = json.TryGetProperty(ResultProperty, out var result) && result.TryGetProperty(UsernameProperty, out var usernameElement)
                ? usernameElement.GetString()
                : null;

            return new CredentialDiagnosis(true, CredentialReasonCodes.Valid, null,
                string.IsNullOrWhiteSpace(username) ? null : new Dictionary<string, string> { [MessagingSetupConstants.FactBotUsername] = username });
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);
        }
        catch (JsonException)
        {
            return new CredentialDiagnosis(false, CredentialReasonCodes.UnexpectedResponse);
        }
    }

    public async Task<TelegramWebhookInfo?> GetWebhookInfoAsync(string configJson, CancellationToken ct = default)
    {
        var config = JsonSerializer.Deserialize<TelegramConfig>(configJson, JsonOptions);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return null;

        try
        {
            var response = await _httpClient.GetAsync(BuildMethodUrl(config.BotToken, GetWebhookInfoMethod), ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            var json = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
            if (!json.TryGetProperty(ResultProperty, out var result) || result.ValueKind != JsonValueKind.Object)
                return null;

            var url = ReadString(result, UrlProperty);
            var pendingUpdateCount = result.TryGetProperty(PendingUpdateCountProperty, out var pendingElement)
                && pendingElement.ValueKind == JsonValueKind.Number
                ? pendingElement.GetInt32()
                : 0;
            var lastErrorMessage = ReadString(result, LastErrorMessageProperty);
            DateTime? lastErrorAtUtc = result.TryGetProperty(LastErrorDateProperty, out var dateElement)
                && dateElement.ValueKind == JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeSeconds(dateElement.GetInt64()).UtcDateTime
                : null;

            return new TelegramWebhookInfo(string.IsNullOrWhiteSpace(url) ? null : url, pendingUpdateCount, lastErrorMessage, lastErrorAtUtc);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    private static string BuildMethodUrl(string token, string method) => $"https://api.telegram.org/bot{token}/{method}";

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
