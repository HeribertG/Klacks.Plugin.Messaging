// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record TelegramWebhookInfo(string? Url, int PendingUpdateCount, string? LastErrorMessage, DateTime? LastErrorAtUtc);
