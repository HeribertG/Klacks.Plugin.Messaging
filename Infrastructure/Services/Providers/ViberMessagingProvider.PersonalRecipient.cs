// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Implements IPersonalRecipientClassifier for Viber: every non-empty recipient (a Viber user id) is
/// personal, since Viber bot conversations are always 1:1.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class ViberMessagingProvider : IPersonalRecipientClassifier
{
    public bool IsPersonalRecipient(string recipient) => !string.IsNullOrWhiteSpace(recipient);
}
