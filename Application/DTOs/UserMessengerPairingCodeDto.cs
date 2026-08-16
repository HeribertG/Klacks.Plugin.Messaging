// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Application.DTOs;

public class UserMessengerPairingCodeDto
{
    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
