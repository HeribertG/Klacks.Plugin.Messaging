// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Application.DTOs;

public class UserMessengerContactDto
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public MessengerType Type { get; set; }

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsPreferred { get; set; }
}
