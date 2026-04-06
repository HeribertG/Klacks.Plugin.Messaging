// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Default implementation of IOwnerMessengerReader. Reads the APP_OWNER_MESSENGERS jsonb
/// setting via IPluginSettingsReader and parses it into OwnerMessengerEntry records.
/// </summary>
using System.Text.Json;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class OwnerMessengerReader : IOwnerMessengerReader
{
    private const string OwnerNameSettingKey = "APP_ADDRESS_NAME";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPluginSettingsReader _settingsReader;
    private readonly ILogger<OwnerMessengerReader> _logger;

    public OwnerMessengerReader(IPluginSettingsReader settingsReader, ILogger<OwnerMessengerReader> logger)
    {
        _settingsReader = settingsReader;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OwnerMessengerEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var raw = await _settingsReader.GetSettingAsync(MessagingConstants.SettingOwnerMessengers);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<OwnerMessengerEntry>();

        try
        {
            var list = JsonSerializer.Deserialize<List<OwnerMessengerEntry>>(raw, JsonOptions);
            return list ?? (IReadOnlyList<OwnerMessengerEntry>)Array.Empty<OwnerMessengerEntry>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse APP_OWNER_MESSENGERS jsonb. Returning empty list.");
            return Array.Empty<OwnerMessengerEntry>();
        }
    }

    public async Task<OwnerMessengerEntry?> GetByTypeAsync(MessengerType type, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(e => e.Type == type && !string.IsNullOrWhiteSpace(e.Value));
    }

    public async Task<string?> GetOwnerDisplayNameAsync(CancellationToken ct = default)
    {
        return await _settingsReader.GetSettingAsync(OwnerNameSettingKey);
    }
}
