// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Static parser helpers for Telegram webhook update payloads.
/// Extracts well-known fields defensively without throwing on missing members.
/// </summary>

using System.Text.Json;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

internal static class TelegramWebhookPayload
{
    public static bool TryGetMessageElement(JsonElement root, out JsonElement message)
        => root.TryGetProperty("message", out message);

    public static string GetText(JsonElement message)
        => message.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;

    public static string GetMessageId(JsonElement message)
        => message.TryGetProperty("message_id", out var id) ? id.ToString() : string.Empty;

    public static string GetSenderId(JsonElement message)
        => TryGetFromField(message, "id", out var id) ? id : string.Empty;

    public static string GetSenderFirstName(JsonElement message)
        => TryGetFromField(message, "first_name", out var name) ? name : string.Empty;

    public static string? GetChatId(JsonElement message)
    {
        if (!message.TryGetProperty("chat", out var chat))
            return null;
        if (!chat.TryGetProperty("id", out var idEl))
            return null;

        return idEl.ValueKind == JsonValueKind.Number
            ? idEl.GetInt64().ToString()
            : idEl.GetString();
    }

    private static bool TryGetFromField(JsonElement message, string field, out string value)
    {
        value = string.Empty;
        if (!message.TryGetProperty("from", out var from) || from.ValueKind == JsonValueKind.Undefined)
            return false;
        if (!from.TryGetProperty(field, out var el))
            return false;

        value = el.ValueKind == JsonValueKind.Number ? el.ToString() : el.GetString() ?? string.Empty;
        return true;
    }
}
