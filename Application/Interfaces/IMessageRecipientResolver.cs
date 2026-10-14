// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Resolves the recipient of a send_message call to the provider and address a message would be sent to, applying the rules of
/// the messaging routes: an administrator reaches the owner, application users and raw addresses; every other caller reaches only
/// the contacts of clients its group visibility lets it see, and a hidden client is answered like a missing contact. The
/// single source of the resolution for the skill that sends and for the preview the user confirms.
/// </summary>
/// <param name="parameters">The arguments of the call</param>
/// <param name="isAdmin">Whether the caller holds the administrator role</param>

using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessageRecipientResolver
{
    Task<MessageRecipientResolution> ResolveAsync(
        SendMessageParameters parameters,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
