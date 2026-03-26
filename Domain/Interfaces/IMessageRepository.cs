// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository interface for message persistence operations.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid? providerId, MessageDirection? direction, string? sender, int count, int offset);

    Task<int> GetMessageCountAsync(Guid providerId);

    Task AddAsync(Message message);

    Task DeleteOldestMessagesAsync(Guid providerId, int retainCount);
}
