// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Optional capability of a messaging provider adapter: tells whether a stored recipient identifier
/// addresses one person privately (a user id or phone number) rather than a group, channel or room.
/// The core's inbound clarification dialog only sends a question to a personal address. An adapter
/// that does not implement this interface counts as "cannot tell", which the caller treats as "no".
/// Same optional-capability pattern as IPairingInstructionsProvider and IWebhookRegistrar.
/// </summary>
/// <param name="recipient">The stored MessengerContact value</param>

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IPersonalRecipientClassifier
{
    bool IsPersonalRecipient(string recipient);
}
