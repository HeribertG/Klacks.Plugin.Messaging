// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// PostgreSQL implementation of IBroadcastRunLock with a session-level advisory lock. A transaction-level lock does not fit: the run
/// commits per recipient and calls external services for up to two minutes. An in-process semaphore does not fit either: a second
/// server process on the same database would not see it. The lock lives on the connection, so the connection of the scoped context
/// stays open from the first acquire to the last release, and the database frees the lock by itself when a process dies. Providers
/// other than Npgsql (the in-memory test store) have no such lock and grant it. Scoped: the hold count is the re-entrancy of one
/// request, not shared state.
/// </summary>
/// <param name="context">The scoped context whose connection carries the lock</param>
/// <param name="logger">Records a release that failed</param>

using System.Data.Common;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public sealed class BroadcastRunLock : IBroadcastRunLock
{
    private const string LockKey = "7203041955100002";
    private const string TryLockSql = "SELECT pg_try_advisory_lock(" + LockKey + ")";
    private const string UnlockSql = "SELECT pg_advisory_unlock(" + LockKey + ")";

    private readonly DbContext _context;
    private readonly ILogger<BroadcastRunLock> _logger;
    private int _holds;

    public BroadcastRunLock(DbContext context, ILogger<BroadcastRunLock> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct)
    {
        if (_holds == 0 && _context.Database.IsNpgsql() && !await TryLockAsync(ct))
        {
            return null;
        }

        _holds++;
        return new Hold(this);
    }

    private async Task<bool> TryLockAsync(CancellationToken ct)
    {
        await _context.Database.OpenConnectionAsync(ct);

        var acquired = false;
        try
        {
            acquired = await ExecuteAsync(TryLockSql, ct);
            return acquired;
        }
        finally
        {
            if (!acquired)
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task ReleaseAsync()
    {
        _holds--;
        if (_holds > 0 || !_context.Database.IsNpgsql())
        {
            return;
        }

        try
        {
            await ExecuteAsync(UnlockSql, CancellationToken.None);
        }
        catch (DbException ex)
        {
            _logger.LogWarning(ex, "Releasing the broadcast run lock failed; it ends with its connection");
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private sealed class Hold(BroadcastRunLock owner) : IAsyncDisposable
    {
        private bool _released;

        public async ValueTask DisposeAsync()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            await owner.ReleaseAsync();
        }
    }
}
