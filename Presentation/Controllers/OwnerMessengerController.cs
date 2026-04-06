// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// REST controller for the owner messenger jsonb setting (APP_OWNER_MESSENGERS).
/// Used by the Owner-Messenger card in Settings to read and write the owner's
/// per-provider chat IDs.
/// </summary>
using System.Text.Json;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/owner-messengers")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
public class OwnerMessengerController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IOwnerMessengerReader _reader;
    private readonly IPluginSettingsWriter _writer;

    public OwnerMessengerController(IOwnerMessengerReader reader, IPluginSettingsWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerMessengerEntry>>> Get(CancellationToken ct)
    {
        var entries = await _reader.GetAllAsync(ct);
        return Ok(entries);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<OwnerMessengerEntry>>> Put([FromBody] List<OwnerMessengerEntry> entries, CancellationToken ct)
    {
        var sanitized = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Value))
            .Select(e => new OwnerMessengerEntry
            {
                Type = e.Type,
                Value = e.Value.Trim(),
                Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description.Trim()
            })
            .ToList();

        var json = JsonSerializer.Serialize(sanitized, JsonOptions);
        await _writer.SetSettingAsync(MessagingConstants.SettingOwnerMessengers, json, ct);

        return Ok(sanitized);
    }
}
