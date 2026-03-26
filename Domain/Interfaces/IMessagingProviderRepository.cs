// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository interface for messaging provider persistence operations.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessagingProviderRepository
{
    Task<MessagingProvider?> GetByIdAsync(Guid id);

    Task<MessagingProvider?> GetByNameAsync(string name);

    Task<IReadOnlyList<MessagingProvider>> GetAllAsync();

    Task<IReadOnlyList<MessagingProvider>> GetEnabledAsync();

    Task AddAsync(MessagingProvider provider);

    Task DeleteAsync(Guid id);
}
