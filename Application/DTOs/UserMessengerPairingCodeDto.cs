// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

namespace Klacks.Plugin.Messaging.Application.DTOs;

public class UserMessengerPairingCodeDto
{
    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
