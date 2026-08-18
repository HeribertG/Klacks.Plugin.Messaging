// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants for the user-level messenger pairing flow: code shape, lifetime, retention and the
/// settings key the pending codes are kept under.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class UserMessengerPairingConstants
{
    /// <summary>
    /// Alphabet without the characters a reader confuses when copying by hand: I, O, 0 and 1 are
    /// absent, so a code read off the screen cannot be mistyped into a different valid code.
    /// </summary>
    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Eight characters out of a 32 character alphabet are 40 bits of entropy, which is far more than
    /// a window of minutes needs and still short enough to type into a chat by hand.
    /// </summary>
    public const int CodeLength = 8;

    /// <summary>
    /// Deliberately short: the code travels from the screen into a chat window within seconds, and a
    /// code that outlives that trip is only an unnecessary window for someone else to redeem it.
    /// </summary>
    public const int CodeLifetimeMinutes = 15;

    /// <summary>
    /// Deliberately much longer than the self-service window: an admin-issued invite travels by
    /// email, and the recipient may not check their inbox for hours. Long enough to comfortably
    /// span a weekend, short enough to not become a standing cross-account credential.
    /// </summary>
    public const int AdminInviteCodeLifetimeHours = 72;

    /// <summary>
    /// How long a used or expired record is kept after its expiry. Without this grace the record
    /// would simply vanish and a user who is late could no longer be told 'expired' rather than
    /// 'unknown', which is exactly the difference that tells them to request a new code.
    /// </summary>
    public const int RecordRetentionHours = 24;

    /// <summary>
    /// JSONB settings key holding the pending pairing codes.
    /// Shape: [{ "code": string, "userId": string, "type": int (MessengerType),
    /// "issuedAt": iso, "expiresAt": iso, "usedAt": iso? }]
    /// </summary>
    public const string SettingPendingPairingCodes = "MESSAGING_USER_PAIRING_CODES";

    /// <summary>
    /// Description stored on a contact that was created through the pairing flow, so a channel that
    /// arrived by code is distinguishable from one an administrator entered.
    /// </summary>
    public const string PairedContactDescription = "Messenger pairing";
}
