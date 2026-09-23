// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Admin-only REST endpoint returning the deterministic messenger setup diagnosis.
/// </summary>
/// <param name="diagnostics">Composes the setup report for all configured providers</param>
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/messaging")]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = MessagingConstants.RoleAdmin)]
public class MessagingSetupController : ControllerBase
{
    private readonly IMessagingSetupDiagnosticsService _diagnostics;

    public MessagingSetupController(IMessagingSetupDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    [HttpGet("setup-diagnosis")]
    public async Task<ActionResult<MessagingSetupReport>> GetDiagnosis(CancellationToken ct)
    {
        return Ok(await _diagnostics.DiagnoseAsync(ct));
    }
}
