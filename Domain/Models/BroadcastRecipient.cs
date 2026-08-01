// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// A single resolved recipient of a broadcast.
/// </summary>
/// <param name="ClientId">The client this message is addressed to</param>
/// <param name="Recipient">The provider-specific address the message is sent to</param>
/// <param name="DisplayName">Optional human-readable name for the message log</param>
/// <param name="FromPhoneFallback">True when no messenger contact existed and the client's
/// mobile number was used instead</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record BroadcastRecipient(
    Guid ClientId,
    string Recipient,
    string? DisplayName,
    bool FromPhoneFallback);
