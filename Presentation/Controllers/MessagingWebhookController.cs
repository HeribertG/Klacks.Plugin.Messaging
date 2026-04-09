// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Webhook endpoint for receiving incoming messages from external messaging providers.
/// Handles webhook verification and incoming message processing.
/// </summary>
using System.Text.Json;
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
    private readonly ITelegramOnboardingRedemptionService _redemptionService;
    private readonly ILogger<MessagingWebhookController> _logger;

    public MessagingWebhookController(
        IMessagingService messagingService,
        IMessagingProviderRepository providerRepository,
        IPluginEventBus eventBus,
        ITelegramOnboardingRedemptionService redemptionService,
        ILogger<MessagingWebhookController> logger)
    {
        _messagingService = messagingService;
        _providerRepository = providerRepository;
        _eventBus = eventBus;
        _redemptionService = redemptionService;
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

        if (string.Equals(providerName, TelegramOnboardingConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            var redeemed = await TryHandleStartCommandAsync(body, HttpContext.RequestAborted);
            if (redeemed)
                return Ok();
        }

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

    private async Task<bool> TryHandleStartCommandAsync(string body, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("message", out var message))
                return false;
            if (!message.TryGetProperty("text", out var textEl))
                return false;
            var text = textEl.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (!text.StartsWith(TelegramOnboardingConstants.StartCommandPrefix, StringComparison.Ordinal))
                return false;

            var token = text[TelegramOnboardingConstants.StartCommandPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (!message.TryGetProperty("chat", out var chat))
                return false;
            if (!chat.TryGetProperty("id", out var chatIdEl))
                return false;

            var chatId = chatIdEl.ValueKind == JsonValueKind.Number
                ? chatIdEl.GetInt64().ToString()
                : chatIdEl.GetString();
            if (string.IsNullOrWhiteSpace(chatId))
                return false;

            var result = await _redemptionService.RedeemAsync(token, chatId, ct);
            _logger.LogInformation(
                "Telegram onboarding redemption result {Result} for chat {ChatId}",
                result,
                chatId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed parsing Telegram /start payload");
            return false;
        }
    }
}
