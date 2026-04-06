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

    Task<IReadOnlyList<MessengerContact>> SearchByClientNameAsync(string nameQuery, MessengerType type, CancellationToken ct = default);

    Task AddAsync(MessengerContact contact, CancellationToken ct = default);

    Task UpdateAsync(MessengerContact contact, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
