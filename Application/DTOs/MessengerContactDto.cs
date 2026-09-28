// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Read DTO for MessengerContact returned by the REST controller.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Application.DTOs;

public class MessengerContactDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public MessengerType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}
