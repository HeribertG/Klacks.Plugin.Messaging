// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Write DTO for creating or updating a MessengerContact via the REST controller.
/// </summary>
using System.ComponentModel.DataAnnotations;
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Application.DTOs;

public class CreateMessengerContactDto
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public MessengerType Type { get; set; }

    [Required]
    [StringLength(200)]
    public string Value { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }
}
