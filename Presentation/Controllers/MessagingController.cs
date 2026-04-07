// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// REST API controller for managing messaging providers and messages.
/// Provides CRUD operations for providers and message send/list functionality.
/// </summary>
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.DTOs;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/messaging")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
public class MessagingController : ControllerBase
{
    private readonly IMessagingService _messagingService;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IPluginUnitOfWork _unitOfWork;

    public MessagingController(
        IMessagingService messagingService,
        IMessagingProviderRepository providerRepository,
        IPluginUnitOfWork unitOfWork)
    {
        _messagingService = messagingService;
        _providerRepository = providerRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<MessagingProviderDto>>> GetProviders()
    {
        var providers = await _providerRepository.GetAllAsync();
        return Ok(providers.Select(p => ToDto(p)).ToList());
    }

    [HttpGet("providers/{id:guid}")]
    public async Task<ActionResult<MessagingProviderDto>> GetProvider(Guid id)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null) return NotFound();
        return Ok(ToDto(provider));
    }

    [HttpPost("providers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MessagingProviderDto>> CreateProvider([FromBody] CreateMessagingProviderDto dto)
    {
        var existing = await _providerRepository.GetByNameAsync(dto.Name);
        if (existing != null) return Conflict("A provider with this name already exists.");

        var provider = new MessagingProvider
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            ProviderType = dto.ProviderType,
            IsEnabled = dto.IsEnabled,
            ConfigJson = dto.ConfigJson,
            WebhookSecret = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _providerRepository.AddAsync(provider);
        await _unitOfWork.CompleteAsync();

        return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, ToDto(provider));
    }

    [HttpPut("providers/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MessagingProviderDto>> UpdateProvider(Guid id, [FromBody] CreateMessagingProviderDto dto)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null) return NotFound();

        provider.DisplayName = dto.DisplayName;
        provider.ProviderType = dto.ProviderType;
        provider.IsEnabled = dto.IsEnabled;
        provider.ConfigJson = dto.ConfigJson;
        provider.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return Ok(ToDto(provider));
    }

    [HttpDelete("providers/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProvider(Guid id)
    {
        await _providerRepository.DeleteAsync(id);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    [HttpPost("providers/{id:guid}/test")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> TestProvider(Guid id)
    {
        var success = await _messagingService.TestProviderAsync(id);
        return Ok(new { Success = success });
    }

    [HttpGet("messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetMessages(
        [FromQuery] Guid? providerId = null,
        [FromQuery] MessageDirection? direction = null,
        [FromQuery] string? sender = null,
        [FromQuery] int count = 50,
        [FromQuery] int offset = 0)
    {
        var messages = await _messagingService.GetMessagesAsync(providerId, direction, sender, count, offset);
        return Ok(messages.Select(m => ToMessageDto(m)).ToList());
    }

    [HttpGet("messages/{id:guid}")]
    public async Task<ActionResult<MessageDto>> GetMessage(Guid id)
    {
        var message = await _messagingService.GetMessageAsync(id);
        if (message == null) return NotFound();
        return Ok(ToMessageDto(message));
    }

    [HttpPost("messages/send")]
    public async Task<ActionResult<SendMessageResult>> SendMessage([FromBody] SendMessageDto dto)
    {
        var request = new SendMessageRequest(dto.Recipient, dto.Content, dto.ContentType, dto.MediaUrl);
        var result = await _messagingService.SendMessageAsync(dto.Provider, request);
        return Ok(result);
    }

    [HttpGet("broadcast/preview")]
    public async Task<ActionResult<BroadcastPreview>> PreviewBroadcast([FromQuery] string provider, [FromQuery] Guid groupId)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { error = "provider is required" });

        if (groupId == Guid.Empty)
            return BadRequest(new { error = "groupId is required" });

        try
        {
            var preview = await _messagingService.PreviewBroadcastAsync(provider, groupId);
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("broadcast/send")]
    public async Task<ActionResult<BroadcastSendResult>> SendBroadcast([FromBody] SendBroadcastDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Provider))
            return BadRequest(new { error = "provider is required" });

        if (dto.GroupId == Guid.Empty)
            return BadRequest(new { error = "groupId is required" });

        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "content is required" });

        try
        {
            var result = await _messagingService.SendBroadcastAsync(dto.Provider, dto.GroupId, dto.Content, dto.ContentType ?? "text");
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static MessagingProviderDto ToDto(MessagingProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        DisplayName = p.DisplayName,
        ProviderType = p.ProviderType,
        IsEnabled = p.IsEnabled,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static MessageDto ToMessageDto(Message m) => new()
    {
        Id = m.Id,
        ProviderId = m.ProviderId,
        ProviderName = m.Provider?.Name ?? string.Empty,
        ExternalMessageId = m.ExternalMessageId,
        Sender = m.Sender,
        SenderDisplayName = m.SenderDisplayName,
        Recipient = m.Recipient,
        RecipientDisplayName = m.RecipientDisplayName,
        Content = m.Content,
        ContentType = m.ContentType,
        Direction = m.Direction,
        Status = m.Status,
        Timestamp = m.Timestamp,
        ErrorMessage = m.ErrorMessage,
        MediaUrl = m.MediaUrl
    };
}
