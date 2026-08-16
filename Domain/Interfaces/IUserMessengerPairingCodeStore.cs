// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Store for the short-lived codes that pair a messenger identity with an application user.
/// Split into a peek and a separate mark-used step on purpose: a code must only be burned once the
/// contact was really created, otherwise a pairing that was refused for a good reason would also
/// cost the user their code and force a second round trip through the bot.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IUserMessengerPairingCodeStore
{
    /// <summary>
    /// Issues a code for the given user. Any earlier unused code of the same user and messenger is
    /// dropped, so a user always has exactly one code in flight and an abandoned one cannot be
    /// redeemed later by whoever happened to see it.
    /// </summary>
    Task<UserMessengerPairingCode> IssueAsync(string userId, MessengerType type, CancellationToken ct = default);

    /// <summary>
    /// Looks a code up without consuming it, reporting unknown, expired and already used separately.
    /// </summary>
    Task<UserMessengerPairingLookup> PeekAsync(string code, MessengerType type, CancellationToken ct = default);

    /// <summary>
    /// Marks a code as redeemed. Called only after the contact was created, which is what makes the
    /// code single-use.
    /// </summary>
    Task MarkUsedAsync(string code, CancellationToken ct = default);
}
