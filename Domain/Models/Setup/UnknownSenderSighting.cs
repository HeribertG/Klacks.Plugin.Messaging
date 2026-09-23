// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record UnknownSenderSighting(string SenderId, string? DisplayName, DateTime SeenAtUtc);
