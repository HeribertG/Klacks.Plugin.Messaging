// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository interface for UserMessengerContact persistence operations.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IUserMessengerContactRepository
{
    Task<UserMessengerContact?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<UserMessengerContact>> GetByUserIdAsync(string userId, CancellationToken ct = default);

    Task<UserMessengerContact?> GetByUserAndTypeAsync(string userId, MessengerType type, CancellationToken ct = default);

    /// <summary>
    /// Returns the channel marked as preferred, or the oldest one when the user never marked any.
    /// Callers that have to reach exactly one channel use this instead of inventing their own order.
    /// </summary>
    Task<UserMessengerContact?> GetPreferredAsync(string userId, CancellationToken ct = default);

    Task<UserMessengerContact?> GetByTypeAndValueAsync(MessengerType type, string value, CancellationToken ct = default);

    Task AddAsync(UserMessengerContact contact, CancellationToken ct = default);

    Task UpdateAsync(UserMessengerContact contact, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
