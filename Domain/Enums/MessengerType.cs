// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

namespace Klacks.Plugin.Messaging.Domain.Enums;

/// <summary>
/// Messenger identity types stored on Klacks clients and on the owner settings.
/// Distinct from CommunicationTypeEnum (phone/email) - this enum lives in the
/// messaging plugin so the core stays unaware of which messengers exist.
/// </summary>
public enum MessengerType
{
    Telegram = 1,
    WhatsApp = 2,
    Signal = 3,
    Threema = 4,
    Viber = 5,
    Line = 6,
    KakaoTalk = 7,
    WeChat = 8,
    Zalo = 9,
    MicrosoftTeams = 10,
    Slack = 11,
    Sms = 12
}
