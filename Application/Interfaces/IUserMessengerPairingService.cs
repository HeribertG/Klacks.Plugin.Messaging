// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Issues and redeems the codes that link a messenger identity to an application user.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IUserMessengerPairingService
{
    /// <summary>
    /// Issues a code for the given user. There is no parameter for a target user anywhere in this
    /// flow: the caller passes the identity it was authenticated as, so a code for somebody else is
    /// not merely forbidden, it cannot be expressed.
    /// </summary>
    Task<UserMessengerPairingCode> IssueCodeAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Redeems a code against the identity that sent it and creates the messenger contact.
    /// </summary>
    /// <param name="code">The code the user sent to the bot.</param>
    /// <param name="type">Messenger the code arrived on.</param>
    /// <param name="senderValue">Provider-specific sender identifier, e.g. the Telegram chat id.</param>
    Task<OnboardingRedeemResult> RedeemAsync(
        string code,
        MessengerType type,
        string senderValue,
        CancellationToken ct = default);
}
