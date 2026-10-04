// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Narrows the messaging REST surface to what a non-admin caller may see under the host's group visibility.
/// Messages carry chat ids, phone numbers and content of the client they belong to, so a message is visible
/// only when its client is; a message without a client (owner, staff or unknown sender) is never visible here.
/// Admins do not go through this scope at all.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessagingAccessScope
{
    /// <summary>
    /// One page of messages the caller may see, newest page first and each page in ascending time order like
    /// the unrestricted list; offset and count count visible messages only.
    /// </summary>
    /// <param name="providerId">Optional provider filter</param>
    /// <param name="direction">Optional direction filter</param>
    /// <param name="sender">Optional sender address filter</param>
    /// <param name="scope">Optional scope filter; Internal is never visible and yields an empty page</param>
    /// <param name="count">Visible messages to return</param>
    /// <param name="offset">Visible messages to skip</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlyList<Message>> GetVisibleMessagesAsync(
        Guid? providerId, MessageDirection? direction, string? sender, MessageScope? scope, int count, int offset, CancellationToken ct = default);

    Task<bool> IsMessageVisibleAsync(Message message, CancellationToken ct = default);

    Task<bool> IsClientVisibleAsync(Guid? clientId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetVisibleGroupClientIdsAsync(Guid groupId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetVisibleIdNumberClientIdsAsync(IReadOnlyCollection<int> idNumbers, CancellationToken ct = default);
}
