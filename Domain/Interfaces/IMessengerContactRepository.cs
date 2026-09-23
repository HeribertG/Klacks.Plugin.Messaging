// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository interface for MessengerContact persistence operations.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessengerContactRepository
{
    Task<MessengerContact?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<MessengerContact>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);

    Task<MessengerContact?> GetByClientAndTypeAsync(Guid clientId, MessengerType type, CancellationToken ct = default);

    Task<IReadOnlyList<ClientMessengerMatch>> SearchByClientNameAsync(string nameQuery, MessengerType type, CancellationToken ct = default);

    Task<MessengerContact?> GetByTypeAndValueAsync(MessengerType type, string value, CancellationToken ct = default);

    /// <summary>
    /// Counts the distinct clients holding at least one non-deleted contact of the given messenger type.
    /// Counts clients of every entity type, not only employees.
    /// </summary>
    /// <param name="type">Messenger type to count contacts for</param>
    Task<int> CountByTypeAsync(MessengerType type, CancellationToken ct = default);

    Task AddAsync(MessengerContact contact, CancellationToken ct = default);

    Task UpdateAsync(MessengerContact contact, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
