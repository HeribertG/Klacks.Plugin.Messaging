// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Represents a messaging provider configuration (e.g. WhatsApp, Telegram).
/// </summary>
/// <param name="Id">Unique identifier for the provider</param>
/// <param name="Name">Internal name used for lookups</param>
/// <param name="DisplayName">Human-readable name shown in the UI</param>
/// <param name="ProviderType">Type identifier for the provider implementation</param>
/// <param name="ConfigJson">Encrypted JSON containing provider-specific settings</param>
using System.ComponentModel.DataAnnotations;

namespace Klacks.Plugin.Messaging.Domain.Models;

public class MessagingProvider
{
    [Key]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ProviderType { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string ConfigJson { get; set; } = "{}";

    public string WebhookSecret { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
