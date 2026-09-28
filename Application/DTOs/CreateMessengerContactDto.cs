// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

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
