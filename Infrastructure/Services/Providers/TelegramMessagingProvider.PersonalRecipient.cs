// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Implements IPersonalRecipientClassifier for Telegram: a recipient is personal when it parses as a
/// positive chat id. The stored MessengerContact value is the chat id, which for private chats equals
/// the user id; group, supergroup and channel chat ids are negative, and "@username" or "0" are not
/// numeric chat ids at all.
/// </summary>

using System.Globalization;
using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class TelegramMessagingProvider : IPersonalRecipientClassifier
{
    public bool IsPersonalRecipient(string recipient) =>
        !string.IsNullOrWhiteSpace(recipient)
        && long.TryParse(recipient.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId)
        && chatId > 0;
}
