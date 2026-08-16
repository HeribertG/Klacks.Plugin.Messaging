// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// A messenger identity (chat ID, username, or phone) attached to a Klacks application user
/// rather than to a client. Additive to MessengerContact and deliberately separate from it:
/// MessengerContact is bound to a non-nullable ClientId, and an AppUser has no Client, so a
/// planner who has to be reached could not be resolved through it at all.
/// </summary>
/// <param name="Id">Primary key</param>
/// <param name="UserId">AppUser identifier this messenger identity belongs to, stored as text like the Identity key itself</param>
/// <param name="Type">Which messenger this entry is for</param>
/// <param name="Value">The provider-specific identifier (Telegram chat_id, WhatsApp phone, Threema ID, ...)</param>
/// <param name="Description">Optional human-readable note (e.g. 'Privat', 'Geschaeft')</param>
/// <param name="IsPreferred">Marks the one channel to use when a single channel has to be picked</param>
using System.ComponentModel.DataAnnotations;
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models;

public class UserMessengerContact
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public MessengerType Type { get; set; }

    [Required]
    [StringLength(200)]
    public string Value { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    public bool IsPreferred { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public DateTime? UpdateTime { get; set; }
}
