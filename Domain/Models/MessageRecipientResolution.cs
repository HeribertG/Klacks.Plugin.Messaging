// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Outcome of resolving the recipient of a send_message call: either the provider, display name and address the message
/// would go to, or the model-facing reason it cannot go anywhere (no contact, ambiguous name, administrator rights needed).
/// </summary>
/// <param name="Success">True when the recipient was resolved</param>
/// <param name="ProviderName">Provider name or type to hand to the messaging service; set when resolved</param>
/// <param name="DisplayName">Human-readable recipient name; set when resolved</param>
/// <param name="Identifier">The address (chat ID, phone number, ...) the message is sent to; set when resolved</param>
/// <param name="Error">Model-facing reason; set when not resolved</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public sealed record MessageRecipientResolution(
    bool Success,
    string? ProviderName,
    string? DisplayName,
    string? Identifier,
    string? Error)
{
    public static MessageRecipientResolution Resolved(string providerName, string displayName, string identifier) =>
        new(true, providerName, displayName, identifier, null);

    public static MessageRecipientResolution Failed(string error) => new(false, null, null, null, error);
}
