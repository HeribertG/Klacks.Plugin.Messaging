// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// A messenger identity (chat ID, username, or phone) attached to a Klacks client.
/// Stored in a plugin-owned table so the messaging plugin can be uninstalled
/// without leaving foreign-key cruft on the core Client entity.
/// </summary>
/// <param name="Id">Primary key</param>
/// <param name="ClientId">FK to the Klacks Client this messenger identity belongs to</param>
/// <param name="Type">Which messenger this entry is for</param>
/// <param name="Value">The provider-specific identifier (Telegram chat_id, WhatsApp phone, Threema ID, ...)</param>
/// <param name="Description">Optional human-readable note (e.g. 'Privat', 'Geschaeft')</param>
using System.ComponentModel.DataAnnotations;
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models;

public class MessengerContact
{
    [Key]
    public Guid Id { get; set; }

    public Guid ClientId { get; set; }

    public MessengerType Type { get; set; }

    [Required]
    [StringLength(200)]
    public string Value { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public DateTime? UpdateTime { get; set; }
}
