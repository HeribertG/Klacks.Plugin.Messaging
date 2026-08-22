// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Optional adapter capability for building the plain-text instructions a user needs to redeem an
/// AppUser messenger-pairing code on this provider (e.g. a clickable Telegram deep link, or plain
/// instructions naming the Slack bot to message directly). A provider not implementing this cannot
/// support the admin-invite flow at all.
/// </summary>
/// <param name="code">The pairing code the invitee must send to redeem it</param>
/// <param name="configJson">Provider-specific configuration JSON</param>
namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IPairingInstructionsProvider
{
    Task<string?> BuildPairingInstructionsAsync(string code, string configJson, CancellationToken ct = default);
}
