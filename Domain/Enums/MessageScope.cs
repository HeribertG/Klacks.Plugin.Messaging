// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Enums;

/// <summary>
/// Whether a message is tied to a Klacks client or is internal (owner bridge, future user DMs).
/// </summary>
public enum MessageScope
{
    Client,
    Internal
}
