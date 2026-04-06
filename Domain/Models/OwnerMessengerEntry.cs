// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Single entry in the APP_OWNER_MESSENGERS jsonb settings array.
/// Used by SendMessageSkill self-resolution and by the owner-messenger UI card.
/// </summary>
/// <param name="Type">MessengerType (Telegram, WhatsApp, ...)</param>
/// <param name="Value">Provider-specific identifier (chat_id, phone, ...)</param>
/// <param name="Description">Optional human-readable note</param>
using System.Text.Json.Serialization;
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models;

public class OwnerMessengerEntry
{
    [JsonPropertyName("type")]
    public MessengerType Type { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
