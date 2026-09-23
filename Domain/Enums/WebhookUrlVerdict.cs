// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Enums;

public enum WebhookUrlVerdict
{
    Missing,
    Invalid,
    NotHttps,
    NotPublic,
    Public
}
