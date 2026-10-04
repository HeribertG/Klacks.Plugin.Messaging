// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// REST API controller for managing messaging providers and messages.
/// Provides CRUD operations for providers and message send/list functionality.
/// Messages and broadcasts reach clients' chat ids, phone numbers and conversations, so every message route is
/// limited to admins and supervisors, and a supervisor only sees, answers and broadcasts to clients of its visible
/// groups; a hidden client is answered exactly like a missing one. Provider reads stay open to every signed-in
/// user because they carry no configuration or secret and the client form asks them for every user.
/// </summary>
/// <param name="messagingService">Sends, lists and broadcasts messages</param>
/// <param name="providerRepository">Persistence of the provider configuration</param>
/// <param name="unitOfWork">Commits provider writes</param>
/// <param name="rolloutTrigger">Starts the Telegram onboarding rollout when Telegram becomes enabled</param>
/// <param name="accessScope">The host's group visibility applied to messages and broadcast audiences</param>
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.DTOs;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Services;
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
    private readonly ITelegramRolloutTrigger _rolloutTrigger;
    private readonly IMessagingAccessScope _accessScope;

    public MessagingController(
        IMessagingService messagingService,
        IMessagingProviderRepository providerRepository,
        IPluginUnitOfWork unitOfWork,
        ITelegramRolloutTrigger rolloutTrigger,
        IMessagingAccessScope accessScope)
    {
        _messagingService = messagingService;
        _providerRepository = providerRepository;
        _unitOfWork = unitOfWork;
        _rolloutTrigger = rolloutTrigger;
        _accessScope = accessScope;
    }

    private bool IsAdmin => User.IsInRole(MessagingConstants.RoleAdmin);

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
    [Authorize(Roles = MessagingConstants.RoleAdmin)]
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

        await _messagingService.RegisterWebhookAsync(provider.Id);

        return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, ToDto(provider));
    }

    [HttpPut("providers/{id:guid}")]
    [Authorize(Roles = MessagingConstants.RoleAdmin)]
    public async Task<ActionResult<MessagingProviderDto>> UpdateProvider(Guid id, [FromBody] CreateMessagingProviderDto dto)
    {
        var provider = await _providerRepository.GetByIdAsync(id);
        if (provider == null) return NotFound();

        if (!MessagingProviderConfigMerger.TryMerge(provider.ConfigJson, dto.ConfigJson, out var mergedConfig))
        {
            return BadRequest("ConfigJson must be a JSON object.");
        }

        var wasTelegramEnabled = IsTelegramEnabled(provider.ProviderType, provider.IsEnabled);

        provider.DisplayName = dto.DisplayName;
        provider.ProviderType = dto.ProviderType;
        provider.IsEnabled = dto.IsEnabled;
        provider.ConfigJson = mergedConfig;
        provider.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();

        await _messagingService.RegisterWebhookAsync(provider.Id);

        var isNowTelegramEnabled = IsTelegramEnabled(dto.ProviderType, dto.IsEnabled);
        await _rolloutTrigger.TriggerIfNewlyEnabledAsync(wasTelegramEnabled, isNowTelegramEnabled, provider.ConfigJson);

        return Ok(ToDto(provider));
    }

    private static bool IsTelegramEnabled(string providerType, bool isEnabled)
        => isEnabled && string.Equals(providerType, MessagingConstants.ProviderTelegram, StringComparison.OrdinalIgnoreCase);

    [HttpDelete("providers/{id:guid}")]
    [Authorize(Roles = MessagingConstants.RoleAdmin)]
    public async Task<IActionResult> DeleteProvider(Guid id)
    {
        await _providerRepository.DeleteAsync(id);
        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    [HttpPost("providers/{id:guid}/test")]
    [Authorize(Roles = MessagingConstants.RoleAdmin)]
    public async Task<ActionResult<object>> TestProvider(Guid id)
    {
        var success = await _messagingService.TestProviderAsync(id);
        return Ok(new { Success = success });
    }

    [HttpGet("messages")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetMessages(
        [FromQuery] Guid? providerId = null,
        [FromQuery] MessageDirection? direction = null,
        [FromQuery] string? sender = null,
        [FromQuery] MessageScope? scope = null,
        [FromQuery] int count = 50,
        [FromQuery] int offset = 0)
    {
        var messages = IsAdmin
            ? await _messagingService.GetMessagesAsync(providerId, direction, sender, scope, count, offset)
            : await _accessScope.GetVisibleMessagesAsync(providerId, direction, sender, scope, count, offset);
        return Ok(messages.Select(m => ToMessageDto(m)).ToList());
    }

    [HttpGet("messages/{id:guid}")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<MessageDto>> GetMessage(Guid id)
    {
        var message = await _messagingService.GetMessageAsync(id);
        if (message == null) return NotFound();
        if (!IsAdmin && !await _accessScope.IsMessageVisibleAsync(message)) return NotFound();
        return Ok(ToMessageDto(message));
    }

    [HttpPost("messages/send")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<SendMessageResult>> SendMessage([FromBody] SendMessageDto dto)
    {
        if (!IsAdmin)
        {
            var recipientClientId = await _messagingService.ResolveRecipientClientIdAsync(dto.Provider, dto.Recipient);
            if (!await _accessScope.IsClientVisibleAsync(recipientClientId)) return NotFound();
        }

        var request = new SendMessageRequest(dto.Recipient, dto.Content, dto.ContentType, dto.MediaUrl);
        var result = await _messagingService.SendMessageAsync(dto.Provider, request);
        return Ok(result);
    }

    [HttpGet("broadcast/preview")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<BroadcastPreview>> PreviewBroadcast([FromQuery] string provider, [FromQuery] Guid groupId)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { error = "provider is required" });

        if (groupId == Guid.Empty)
            return BadRequest(new { error = "groupId is required" });

        try
        {
            var preview = IsAdmin
                ? await _messagingService.PreviewBroadcastAsync(provider, groupId)
                : await _messagingService.PreviewBroadcastToClientsAsync(
                    provider, await _accessScope.GetVisibleGroupClientIdsAsync(groupId));
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("broadcast/send")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
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
            var contentType = dto.ContentType ?? MessagingConstants.DefaultContentType;
            var result = IsAdmin
                ? await _messagingService.SendBroadcastAsync(dto.Provider, dto.GroupId, dto.Content, contentType)
                : await _messagingService.SendBroadcastToClientsAsync(
                    dto.Provider,
                    await _accessScope.GetVisibleGroupClientIdsAsync(dto.GroupId),
                    dto.Content,
                    contentType,
                    MessagingConstants.BroadcastGroupEmptyError);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("broadcast/preview-by-id-numbers")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<BroadcastPreview>> PreviewBroadcastToIdNumbers(
        [FromQuery] string provider,
        [FromQuery] int[] idNumbers)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { error = "provider is required" });

        if (idNumbers.Length == 0)
            return BadRequest(new { error = "idNumbers is required" });

        try
        {
            if (IsAdmin)
            {
                return Ok(await _messagingService.PreviewBroadcastToIdNumbersAsync(provider, idNumbers));
            }

            var visibleClientIds = await _accessScope.GetVisibleIdNumberClientIdsAsync(idNumbers);
            if (visibleClientIds.Count == 0)
                return BadRequest(new { error = MessagingConstants.BroadcastNoClientsForIdNumbersError });

            return Ok(await _messagingService.PreviewBroadcastToClientsAsync(provider, visibleClientIds));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("broadcast/send-to-id-numbers")]
    [Authorize(Roles = MessagingConstants.RolesClientEditors)]
    public async Task<ActionResult<BroadcastSendResult>> SendBroadcastToIdNumbers([FromBody] SendBroadcastToIdNumbersDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Provider))
            return BadRequest(new { error = "provider is required" });

        if (dto.IdNumbers.Length == 0)
            return BadRequest(new { error = "idNumbers is required" });

        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "content is required" });

        try
        {
            var contentType = dto.ContentType ?? MessagingConstants.DefaultContentType;
            var result = IsAdmin
                ? await _messagingService.SendBroadcastToIdNumbersAsync(dto.Provider, dto.IdNumbers, dto.Content, contentType)
                : await _messagingService.SendBroadcastToClientsAsync(
                    dto.Provider,
                    await _accessScope.GetVisibleIdNumberClientIdsAsync(dto.IdNumbers),
                    dto.Content,
                    contentType,
                    MessagingConstants.BroadcastNoClientsForIdNumbersError);
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
