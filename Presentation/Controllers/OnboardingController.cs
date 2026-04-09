// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Endpoint to manually trigger a single Telegram onboarding invitation for an Employee client.
/// Reads the first enabled Telegram provider from persistence and delegates to IOnboardingSendService.
/// </summary>
/// <param name="sendService">Per-client invitation dispatcher.</param>
/// <param name="providerRepository">Messaging provider repository used to resolve the Telegram provider.</param>

using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/messaging/onboarding")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingSendService _sendService;
    private readonly IMessagingProviderRepository _providerRepository;

    public OnboardingController(
        IOnboardingSendService sendService,
        IMessagingProviderRepository providerRepository)
    {
        _sendService = sendService;
        _providerRepository = providerRepository;
    }

    [HttpPost("telegram/send/{clientId:guid}")]
    public async Task<ActionResult<object>> SendTelegramInvitation(Guid clientId, CancellationToken ct)
    {
        var providers = await _providerRepository.GetEnabledAsync();
        var telegram = providers.FirstOrDefault(p =>
            string.Equals(p.ProviderType, MessagingConstants.ProviderTelegram, StringComparison.OrdinalIgnoreCase));
        if (telegram == null)
            return BadRequest(new { result = "NoTelegramProvider" });

        var result = await _sendService.SendAsync(clientId, telegram.ConfigJson, ct);
        return Ok(new { result = result.ToString() });
    }
}
