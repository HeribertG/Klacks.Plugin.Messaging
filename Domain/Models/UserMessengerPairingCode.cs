// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// One issued pairing code. Kept as a value rather than an entity because the store behind it is a
/// settings blob, not a table - see IUserMessengerPairingCodeStore for why.
/// </summary>
/// <param name="Code">The code the user sends to the bot</param>
/// <param name="UserId">AppUser the code was issued for; a code carries its owner so it can never link a foreign account</param>
/// <param name="Type">Messenger the code may be redeemed on</param>
/// <param name="IssuedAt">UTC instant the code was created</param>
/// <param name="ExpiresAt">UTC instant after which the code is no longer redeemable</param>
/// <param name="UsedAt">UTC instant of redemption, or null while unused</param>
/// <param name="IssuedByAdminId">
/// AppUser id of the administrator who issued this code on the owner's behalf, or null for the
/// ordinary self-service code. Kept on the record itself, not a separate log, so the code's origin
/// travels with it for as long as it exists.
/// </param>

using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models;

public record UserMessengerPairingCode(
    string Code,
    string UserId,
    MessengerType Type,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    DateTime? UsedAt,
    string? IssuedByAdminId = null);
