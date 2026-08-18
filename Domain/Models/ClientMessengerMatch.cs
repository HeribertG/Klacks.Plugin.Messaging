// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Keyless projection of a client-name search hit against MessengerContact: the resolved
/// messenger identifier plus enough of the client's name to build a disambiguation message when a
/// search returns more than one candidate.
/// </summary>
/// <param name="ClientId">The matched client's id</param>
/// <param name="Value">The provider-specific messenger identifier (chat ID, phone, ...)</param>
/// <param name="FirstName">Client first name, as stored (may be empty)</param>
/// <param name="Name">Client last name / legal entity name, as stored (may be empty)</param>
/// <param name="Company">Client company name, if any</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public class ClientMessengerMatch
{
    public Guid ClientId { get; set; }

    public string Value { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Company { get; set; }
}
