// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository implementation for messaging provider CRUD operations.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Infrastructure.Repositories;

public class MessagingProviderRepository : IMessagingProviderRepository
{
    private readonly DbContext _context;

    public MessagingProviderRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<MessagingProvider?> GetByIdAsync(Guid id)
    {
        return await _context.Set<MessagingProvider>().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<MessagingProvider?> GetByNameAsync(string name)
    {
        return await _context.Set<MessagingProvider>().FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<IReadOnlyList<MessagingProvider>> GetAllAsync()
    {
        return await _context.Set<MessagingProvider>().AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<MessagingProvider>> GetEnabledAsync()
    {
        return await _context.Set<MessagingProvider>()
            .Where(p => p.IsEnabled)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddAsync(MessagingProvider provider)
    {
        await _context.Set<MessagingProvider>().AddAsync(provider);
    }

    public async Task DeleteAsync(Guid id)
    {
        var provider = await _context.Set<MessagingProvider>().FirstOrDefaultAsync(p => p.Id == id);
        if (provider != null)
        {
            _context.Set<MessagingProvider>().Remove(provider);
        }
    }
}
