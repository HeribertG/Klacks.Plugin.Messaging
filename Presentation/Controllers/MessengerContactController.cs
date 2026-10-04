// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// REST API controller for managing per-client messenger contacts (Telegram chat IDs,
/// WhatsApp numbers, Threema IDs, ...). Used by the Messenger tab in Mitarbeiter-Edit.
/// A messenger contact decides which client an inbound chat is attributed to, and inbound automation then
/// acts for that client, so the host's group visibility applies to every route: a contact of a client the
/// caller may not see is answered exactly like a missing one, and only admins and supervisors may write.
/// </summary>
/// <param name="repository">Persistence of the messenger contacts</param>
/// <param name="unitOfWork">Commits the writes</param>
/// <param name="clientVisibility">The host's group visibility for the owning client</param>
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.DTOs;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/messenger-contacts")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
public class MessengerContactController : ControllerBase
{
    private readonly IMessengerContactRepository _repository;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly IClientVisibilityReader _clientVisibility;

    public MessengerContactController(
        IMessengerContactRepository repository,
        IPluginUnitOfWork unitOfWork,
        IClientVisibilityReader clientVisibility)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clientVisibility = clientVisibility;
    }

    [HttpGet("by-client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MessengerContactDto>>> GetByClient(Guid clientId, CancellationToken ct)
    {
        if (!await _clientVisibility.IsClientVisibleAsync(clientId, ct))
        {
            return Ok(new List<MessengerContactDto>());
        }

        var contacts = await _repository.GetByClientIdAsync(clientId, ct);
        return Ok(contacts.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MessengerContactDto>> GetById(Guid id, CancellationToken ct)
    {
        var contact = await LoadVisibleAsync(id, ct);
        if (contact == null) return NotFound();
        return Ok(ToDto(contact));
    }

    [HttpPost]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<MessengerContactDto>> Create([FromBody] CreateMessengerContactDto dto, CancellationToken ct)
    {
        if (!await _clientVisibility.IsClientVisibleAsync(dto.ClientId, ct))
        {
            return NotFound();
        }

        var contact = new MessengerContact
        {
            Id = Guid.NewGuid(),
            ClientId = dto.ClientId,
            Type = dto.Type,
            Value = dto.Value.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsDeleted = false,
            CreateTime = DateTime.UtcNow
        };

        await _repository.AddAsync(contact, ct);
        await _unitOfWork.CompleteAsync();

        return CreatedAtAction(nameof(GetById), new { id = contact.Id }, ToDto(contact));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<MessengerContactDto>> Update(Guid id, [FromBody] CreateMessengerContactDto dto, CancellationToken ct)
    {
        var contact = await LoadVisibleAsync(id, ct);
        if (contact == null) return NotFound();

        contact.Type = dto.Type;
        contact.Value = dto.Value.Trim();
        contact.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        await _repository.UpdateAsync(contact, ct);
        await _unitOfWork.CompleteAsync();

        return Ok(ToDto(contact));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var contact = await LoadVisibleAsync(id, ct);
        if (contact == null) return NotFound();

        await _repository.DeleteAsync(id, ct);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    private async Task<MessengerContact?> LoadVisibleAsync(Guid id, CancellationToken ct)
    {
        var contact = await _repository.GetByIdAsync(id, ct);
        if (contact == null || !await _clientVisibility.IsClientVisibleAsync(contact.ClientId, ct))
        {
            return null;
        }

        return contact;
    }

    private static MessengerContactDto ToDto(MessengerContact contact)
    {
        return new MessengerContactDto
        {
            Id = contact.Id,
            ClientId = contact.ClientId,
            Type = contact.Type,
            Value = contact.Value,
            Description = contact.Description
        };
    }
}
