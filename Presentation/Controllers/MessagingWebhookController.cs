// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Webhook endpoint for receiving incoming messages from external messaging providers.
/// Handles webhook verification and incoming message processing.
/// </summary>
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.DTOs;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/messaging/webhook")]
[AllowAnonymous]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
public class MessagingWebhookController : ControllerBase
{
    private readonly IMessagingService _messagingService;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IPluginEventBus _eventBus;
    private readonly ILogger<MessagingWebhookController> _logger;

    public MessagingWebhookController(
        IMessagingService messagingService,
        IMessagingProviderRepository providerRepository,
        IPluginEventBus eventBus,
        ILogger<MessagingWebhookController> logger)
    {
        _messagingService = messagingService;
        _providerRepository = providerRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    [HttpPost("{providerName}")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> ReceiveWebhook(string providerName)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault()
            ?? Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();

        try
        {
            var message = await _messagingService.ProcessIncomingMessageAsync(providerName, body, signature);
            var provider = await _providerRepository.GetByNameAsync(providerName);

            var notification = new IncomingMessageDto
            {
                MessageId = message.Id,
                ProviderName = provider?.Name ?? providerName,
                ProviderDisplayName = provider?.DisplayName ?? providerName,
                Sender = message.Sender,
                SenderDisplayName = message.SenderDisplayName,
                Content = message.Content,
                ContentType = message.ContentType,
                Timestamp = message.Timestamp
            };

            await _eventBus.BroadcastAsync("messaging.incoming", notification);

            return Ok();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid webhook request for provider {Provider}", providerName);
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("{providerName}")]
    public IActionResult VerifyWebhook(string providerName, [FromQuery(Name = "hub.challenge")] string? challenge = null)
    {
        if (!string.IsNullOrEmpty(challenge) && challenge.Length <= 256)
            return Ok(challenge);

        return Ok("Webhook endpoint active");
    }
}
