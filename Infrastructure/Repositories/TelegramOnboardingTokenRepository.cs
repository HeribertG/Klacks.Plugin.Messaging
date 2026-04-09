// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core repository for TelegramOnboardingToken persistence.
/// </summary>
/// <param name="context">Plugin DbContext used for persistence via the host's unit of work.</param>
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Infrastructure.Repositories;

public class TelegramOnboardingTokenRepository : ITelegramOnboardingTokenRepository
{
    private readonly DbContext _context;

    public TelegramOnboardingTokenRepository(DbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TelegramOnboardingToken token, CancellationToken ct = default)
    {
        await _context.Set<TelegramOnboardingToken>().AddAsync(token, ct);
    }

    public Task<TelegramOnboardingToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return _context.Set<TelegramOnboardingToken>()
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<TelegramOnboardingToken>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        return await _context.Set<TelegramOnboardingToken>()
            .Where(t => t.ClientId == clientId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task InvalidateAllForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var active = await _context.Set<TelegramOnboardingToken>()
            .Where(t => t.ClientId == clientId && t.UsedAt == null && !t.IsDeleted)
            .ToListAsync(ct);
        foreach (var token in active)
            token.IsDeleted = true;
    }

    public Task UpdateAsync(TelegramOnboardingToken token, CancellationToken ct = default)
    {
        _context.Set<TelegramOnboardingToken>().Update(token);
        return Task.CompletedTask;
    }
}
