// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Implements IPersonalRecipientClassifier for WhatsApp: every non-empty recipient (a phone number) is
/// personal, since WhatsApp Cloud API conversations are always 1:1.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class WhatsAppMessagingProvider : IPersonalRecipientClassifier
{
    public bool IsPersonalRecipient(string recipient) => !string.IsNullOrWhiteSpace(recipient);
}
