// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// REST API controller for managing per-client messenger contacts (Telegram chat IDs,
/// WhatsApp numbers, Threema IDs, ...). Used by the Messenger tab in Mitarbeiter-Edit.
/// </summary>
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

    public MessengerContactController(
        IMessengerContactRepository repository,
        IPluginUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("by-client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MessengerContactDto>>> GetByClient(Guid clientId, CancellationToken ct)
    {
        var contacts = await _repository.GetByClientIdAsync(clientId, ct);
        return Ok(contacts.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MessengerContactDto>> GetById(Guid id, CancellationToken ct)
    {
        var contact = await _repository.GetByIdAsync(id, ct);
        if (contact == null) return NotFound();
        return Ok(ToDto(contact));
    }

    [HttpPost]
    public async Task<ActionResult<MessengerContactDto>> Create([FromBody] CreateMessengerContactDto dto, CancellationToken ct)
    {
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
    public async Task<ActionResult<MessengerContactDto>> Update(Guid id, [FromBody] CreateMessengerContactDto dto, CancellationToken ct)
    {
        var contact = await _repository.GetByIdAsync(id, ct);
        if (contact == null) return NotFound();

        contact.Type = dto.Type;
        contact.Value = dto.Value.Trim();
        contact.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        await _repository.UpdateAsync(contact, ct);
        await _unitOfWork.CompleteAsync();

        return Ok(ToDto(contact));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var contact = await _repository.GetByIdAsync(id, ct);
        if (contact == null) return NotFound();

        await _repository.DeleteAsync(id, ct);
        await _unitOfWork.CompleteAsync();
        return NoContent();
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
