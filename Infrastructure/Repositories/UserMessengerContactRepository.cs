// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository implementation for UserMessengerContact CRUD and lookup operations.
/// Uses DbContext.Set&lt;T&gt;() so it works with the host's DataBaseContext via PluginModelRegistry.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Infrastructure.Repositories;

public class UserMessengerContactRepository : IUserMessengerContactRepository
{
    private readonly DbContext _context;

    public UserMessengerContactRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<UserMessengerContact?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<UserMessengerContact>()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<UserMessengerContact>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Array.Empty<UserMessengerContact>();

        return await _context.Set<UserMessengerContact>()
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPreferred)
            .ThenBy(c => c.Type)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<UserMessengerContact?> GetByUserAndTypeAsync(string userId, MessengerType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _context.Set<UserMessengerContact>()
            .Where(c => c.UserId == userId && c.Type == type && !c.IsDeleted)
            .OrderBy(c => c.CreateTime)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserMessengerContact?> GetPreferredAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _context.Set<UserMessengerContact>()
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.IsPreferred)
            .ThenBy(c => c.CreateTime)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserMessengerContact?> GetByTypeAndValueAsync(MessengerType type, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return await _context.Set<UserMessengerContact>()
            .Where(c => c.Type == type && c.Value == value && !c.IsDeleted)
            .OrderBy(c => c.CreateTime)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(UserMessengerContact contact, CancellationToken ct = default)
    {
        await _context.Set<UserMessengerContact>().AddAsync(contact, ct);
    }

    public Task UpdateAsync(UserMessengerContact contact, CancellationToken ct = default)
    {
        contact.UpdateTime = DateTime.UtcNow;
        _context.Set<UserMessengerContact>().Update(contact);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var contact = await _context.Set<UserMessengerContact>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contact != null)
        {
            contact.IsDeleted = true;
            contact.UpdateTime = DateTime.UtcNow;
        }
    }
}
