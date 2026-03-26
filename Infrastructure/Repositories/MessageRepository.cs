// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository implementation for message persistence including filtered queries and retention cleanup.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly DbContext _context;

    public MessageRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Message>()
            .Include(m => m.Provider)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        Guid? providerId, MessageDirection? direction, string? sender, int count, int offset)
    {
        var query = _context.Set<Message>().AsQueryable();

        if (providerId.HasValue)
            query = query.Where(m => m.ProviderId == providerId.Value);

        if (direction.HasValue)
            query = query.Where(m => m.Direction == direction.Value);

        if (!string.IsNullOrWhiteSpace(sender))
            query = query.Where(m => m.Sender == sender);

        return await query
            .OrderByDescending(m => m.Timestamp)
            .Skip(offset)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetMessageCountAsync(Guid providerId)
    {
        return await _context.Set<Message>().CountAsync(m => m.ProviderId == providerId);
    }

    public async Task AddAsync(Message message)
    {
        await _context.Set<Message>().AddAsync(message);
    }

    public async Task DeleteOldestMessagesAsync(Guid providerId, int retainCount)
    {
        var totalCount = await _context.Set<Message>().CountAsync(m => m.ProviderId == providerId);
        if (totalCount <= retainCount)
            return;

        var idsToDelete = await _context.Set<Message>()
            .Where(m => m.ProviderId == providerId)
            .OrderByDescending(m => m.Timestamp)
            .Skip(retainCount)
            .Select(m => m.Id)
            .ToListAsync();

        await _context.Set<Message>()
            .Where(m => idsToDelete.Contains(m.Id))
            .ExecuteDeleteAsync();
    }
}
