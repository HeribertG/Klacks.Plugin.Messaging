// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Repository interface for message persistence operations.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid? providerId, MessageDirection? direction, string? sender, MessageScope? scope, int count, int offset);

    Task<int> GetMessageCountAsync(Guid providerId);

    Task<bool> InboundExistsAsync(Guid providerId, string externalMessageId, CancellationToken ct = default);

    /// <summary>
    /// Clients a broadcast with exactly this text was delivered to on this provider since the given time; failed attempts do not
    /// count. A Pending row counts: it is the claim the delivery commits before each send, so after a crash the send may have gone out.
    /// </summary>
    /// <param name="providerId">Provider of the broadcast</param>
    /// <param name="content">The text, compared exactly</param>
    /// <param name="sinceUtc">Start of the window</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlySet<Guid>> GetRecentBroadcastClientIdsAsync(Guid providerId, string content, DateTime sinceUtc, CancellationToken ct = default);

    Task AddAsync(Message message);

    Task DeleteOldestMessagesAsync(Guid providerId, int retainCount);
}
