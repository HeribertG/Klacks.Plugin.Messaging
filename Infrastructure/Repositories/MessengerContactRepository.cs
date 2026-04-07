// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository implementation for MessengerContact CRUD and lookup operations.
/// Uses DbContext.Set&lt;T&gt;() so it works with the host's DataBaseContext via PluginModelRegistry.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Infrastructure.Repositories;

public class MessengerContactRepository : IMessengerContactRepository
{
    private readonly DbContext _context;

    public MessengerContactRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<MessengerContact?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<MessengerContact>()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<MessengerContact>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        return await _context.Set<MessengerContact>()
            .Where(c => c.ClientId == clientId && !c.IsDeleted)
            .OrderBy(c => c.Type)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<MessengerContact?> GetByClientAndTypeAsync(Guid clientId, MessengerType type, CancellationToken ct = default)
    {
        return await _context.Set<MessengerContact>()
            .Where(c => c.ClientId == clientId && c.Type == type && !c.IsDeleted)
            .OrderBy(c => c.CreateTime)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MessengerContact>> SearchByClientNameAsync(string nameQuery, MessengerType type, CancellationToken ct = default)
    {
        var keywords = nameQuery.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keywords.Length == 0)
            return Array.Empty<MessengerContact>();

        var sql = @"SELECT mc.id, mc.client_id, mc.type, mc.value, mc.description, mc.is_deleted, mc.create_time, mc.update_time
                    FROM messenger_contact mc
                    INNER JOIN client c ON c.id = mc.client_id
                    WHERE mc.is_deleted = false
                      AND mc.type = @p_type
                      AND c.is_deleted = false
                      AND " + string.Join(" AND ", keywords.Select((_, i) => $"(LOWER(COALESCE(c.first_name, '')) LIKE @p_kw{i} OR LOWER(COALESCE(c.name, '')) LIKE @p_kw{i} OR LOWER(COALESCE(c.company, '')) LIKE @p_kw{i})")) + @"
                    ORDER BY mc.create_time
                    LIMIT 5";

        var parameters = new List<Npgsql.NpgsqlParameter> { new Npgsql.NpgsqlParameter("p_type", (int)type) };
        for (var i = 0; i < keywords.Length; i++)
        {
            parameters.Add(new Npgsql.NpgsqlParameter($"p_kw{i}", $"%{keywords[i]}%"));
        }

        return await _context.Set<MessengerContact>()
            .FromSqlRaw(sql, parameters.Cast<object>().ToArray())
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<MessengerContact?> GetByTypeAndValueAsync(MessengerType type, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return await _context.Set<MessengerContact>()
            .Where(c => c.Type == type && c.Value == value && !c.IsDeleted)
            .OrderBy(c => c.CreateTime)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(MessengerContact contact, CancellationToken ct = default)
    {
        await _context.Set<MessengerContact>().AddAsync(contact, ct);
    }

    public Task UpdateAsync(MessengerContact contact, CancellationToken ct = default)
    {
        contact.UpdateTime = DateTime.UtcNow;
        _context.Set<MessengerContact>().Update(contact);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var contact = await _context.Set<MessengerContact>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contact != null)
        {
            contact.IsDeleted = true;
            contact.UpdateTime = DateTime.UtcNow;
        }
    }
}
