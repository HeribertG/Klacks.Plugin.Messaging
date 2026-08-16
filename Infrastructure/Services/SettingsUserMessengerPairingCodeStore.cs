// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Keeps the pending pairing codes in a single plugin-owned settings row rather than in a table of
/// their own. The reason is not elegance: a table would need a database migration, and one unapplied
/// migration already waits in this repository. The settings row is read straight from the database on
/// every access - the host's settings repository holds no cache - so a code issued on one instance is
/// visible to the instance the provider webhook happens to reach, which an in-process cache could
/// never guarantee. What the row cannot give is a compare-and-swap: two users requesting a code in
/// the same instant can overwrite each other, and the loser has to request a second code. That is the
/// price of avoiding the migration and it is documented rather than hidden.
/// </summary>
/// <param name="settingsReader">Reads the pending-codes row.</param>
/// <param name="settingsWriter">Writes the pending-codes row; commits by itself.</param>

using System.Security.Cryptography;
using System.Text.Json;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class SettingsUserMessengerPairingCodeStore : IUserMessengerPairingCodeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IPluginSettingsReader _settingsReader;
    private readonly IPluginSettingsWriter _settingsWriter;

    public SettingsUserMessengerPairingCodeStore(
        IPluginSettingsReader settingsReader,
        IPluginSettingsWriter settingsWriter)
    {
        _settingsReader = settingsReader;
        _settingsWriter = settingsWriter;
    }

    public async Task<UserMessengerPairingCode> IssueAsync(string userId, MessengerType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("A pairing code always belongs to a user", nameof(userId));

        var now = DateTime.UtcNow;
        var issued = new UserMessengerPairingCode(
            GenerateCode(),
            userId,
            type,
            now,
            now.AddMinutes(UserMessengerPairingConstants.CodeLifetimeMinutes),
            UsedAt: null);

        var records = await LoadAsync();
        var kept = records
            .Where(r => !IsSupersededBy(r, issued, now))
            .ToList();
        kept.Add(issued);

        await SaveAsync(kept, ct);

        return issued;
    }

    public async Task<UserMessengerPairingLookup> PeekAsync(string code, MessengerType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return UserMessengerPairingLookup.NotFound;

        var records = await LoadAsync();
        var record = records.FirstOrDefault(r => Matches(r, code) && r.Type == type);

        if (record == null)
            return UserMessengerPairingLookup.NotFound;
        if (record.UsedAt != null)
            return UserMessengerPairingLookup.AlreadyUsed;
        if (record.ExpiresAt < DateTime.UtcNow)
            return UserMessengerPairingLookup.Expired;

        return UserMessengerPairingLookup.Success(record);
    }

    public async Task MarkUsedAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        var records = await LoadAsync();
        var now = DateTime.UtcNow;

        var updated = records
            .Where(r => !IsPrunable(r, now))
            .Select(r => Matches(r, code) && r.UsedAt == null ? r with { UsedAt = now } : r)
            .ToList();

        await SaveAsync(updated, ct);
    }

    /// <summary>
    /// A record leaves the row once it is expired far enough that nobody could still be waiting for
    /// an answer about it. Until then it stays, so 'expired' and 'already used' remain reportable.
    /// </summary>
    private static bool IsPrunable(UserMessengerPairingCode record, DateTime now) =>
        record.ExpiresAt.AddHours(UserMessengerPairingConstants.RecordRetentionHours) < now;

    private static bool IsSupersededBy(UserMessengerPairingCode record, UserMessengerPairingCode issued, DateTime now)
    {
        if (IsPrunable(record, now))
            return true;

        return record.UsedAt == null
            && record.UserId == issued.UserId
            && record.Type == issued.Type;
    }

    private static bool Matches(UserMessengerPairingCode record, string code) =>
        string.Equals(record.Code, code, StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<UserMessengerPairingCode>> LoadAsync()
    {
        var raw = await _settingsReader.GetSettingAsync(UserMessengerPairingConstants.SettingPendingPairingCodes);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<UserMessengerPairingCode>();

        try
        {
            return JsonSerializer.Deserialize<List<UserMessengerPairingCode>>(raw, JsonOptions)
                ?? new List<UserMessengerPairingCode>();
        }
        catch (JsonException)
        {
            // A row that cannot be read must not lock every user out of pairing for good. Treating it
            // as empty costs at most the codes currently in flight, which live minutes anyway.
            return Array.Empty<UserMessengerPairingCode>();
        }
    }

    private async Task SaveAsync(IReadOnlyList<UserMessengerPairingCode> records, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(records, JsonOptions);
        await _settingsWriter.SetSettingAsync(UserMessengerPairingConstants.SettingPendingPairingCodes, json, ct);
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetString(
            UserMessengerPairingConstants.CodeAlphabet,
            UserMessengerPairingConstants.CodeLength);
    }
}
