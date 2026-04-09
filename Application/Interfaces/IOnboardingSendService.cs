// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Sends a single Telegram onboarding invitation to an Employee client.
/// Chooses the channel per client: PrivateCellPhone (SMS) or PrivateMail (Email).
/// </summary>

using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IOnboardingSendService
{
    /// <summary>
    /// Generates a onboarding token, builds a bot deep-link and dispatches it to the client's strict private contact channel.
    /// </summary>
    /// <param name="clientId">Target employee client id.</param>
    /// <param name="botConfigJson">Telegram provider config JSON holding the bot token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OnboardingSendResult> SendAsync(Guid clientId, string botConfigJson, CancellationToken ct = default);
}
