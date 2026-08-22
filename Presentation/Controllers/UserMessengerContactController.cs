// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// REST API for the messenger channels of the signed-in application user: list them, ask for a
/// pairing code, and drop one again. Distinct from MessengerContactController, which manages the
/// channels of a Client - an employee - and is operated by a planner on somebody else's behalf.
/// Every route here derives the user from the access token instead of taking a user id, so the
/// self-service pairing code route has no way to name a foreign account. The read, delete and
/// admin-invite routes an administrator legitimately needs across accounts accept a user id
/// instead, and are role-gated - admin-invite is the one deliberate exception that issues a code
/// for somebody else, see its own doc comment for why.
/// </summary>
/// <param name="repository">Reads and soft-deletes the user-level messenger contacts.</param>
/// <param name="pairingService">Issues the short-lived pairing codes.</param>
/// <param name="unitOfWork">Commits the delete.</param>
/// <param name="providerRepository">Resolves the enabled Telegram provider for the admin-invite email.</param>
/// <param name="inviteSendService">Sends the admin-initiated invite email.</param>

using System.Security.Claims;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Klacks.Plugin.Messaging.Presentation.Controllers;

[ApiController]
[Route("api/user-messenger-contacts")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[RequireFeaturePlugin(MessagingConstants.PluginName)]
public class UserMessengerContactController : ControllerBase
{
    private readonly IUserMessengerContactRepository _repository;
    private readonly IUserMessengerPairingService _pairingService;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IUserInviteSendService _inviteSendService;

    public UserMessengerContactController(
        IUserMessengerContactRepository repository,
        IUserMessengerPairingService pairingService,
        IPluginUnitOfWork unitOfWork,
        IMessagingProviderRepository providerRepository,
        IUserInviteSendService inviteSendService)
    {
        _repository = repository;
        _pairingService = pairingService;
        _unitOfWork = unitOfWork;
        _providerRepository = providerRepository;
        _inviteSendService = inviteSendService;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<UserMessengerContactDto>>> GetMine(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var contacts = await _repository.GetByUserIdAsync(userId, ct);
        return Ok(contacts.Select(ToDto).ToList());
    }

    [HttpGet("by-user/{userId}")]
    [Authorize(Roles = MessagingConstants.RoleAdmin)]
    public async Task<ActionResult<IReadOnlyList<UserMessengerContactDto>>> GetByUser(string userId, CancellationToken ct)
    {
        var contacts = await _repository.GetByUserIdAsync(userId, ct);
        return Ok(contacts.Select(ToDto).ToList());
    }

    /// <summary>
    /// Issues a pairing code for the caller. There is no request body and no user id in the route:
    /// the only account a code can ever be issued for is the one the token authenticated. Provider is
    /// optional: with exactly one enabled messaging provider there is nothing to choose between.
    /// </summary>
    [HttpPost("pairing-code")]
    public async Task<ActionResult<UserMessengerPairingCodeDto>> CreatePairingCode([FromQuery] string? provider, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var resolved = await ResolveEnabledProviderAsync(provider, ct);
        if (resolved.Error != null)
            return BadRequest(new { error = resolved.Error });

        if (!Enum.TryParse<MessengerType>(resolved.Provider!.ProviderType, ignoreCase: true, out var messengerType))
            return BadRequest(new { error = $"Cannot map provider type '{resolved.Provider.ProviderType}' to a messenger type." });

        var issued = await _pairingService.IssueCodeAsync(userId, messengerType, ct);

        return Ok(new UserMessengerPairingCodeDto
        {
            Code = issued.Code,
            ExpiresAt = issued.ExpiresAt
        });
    }

    /// <summary>
    /// Admin-initiated exception to the self-only pairing rule: issues an admin-scoped code for a
    /// different account and emails it with that provider's pairing instructions (a deep link, or
    /// plain text naming the bot), so an admin can actively nudge a user instead of only being able
    /// to point them at their own profile. Deliberate trade-off against the self-only invariant
    /// documented on CreatePairingCode above - Admin-gated and logged with the issuing admin's id for
    /// that reason. Provider is optional: with exactly one enabled messaging provider there is
    /// nothing to choose between.
    /// </summary>
    [HttpPost("admin-invite/{userId}")]
    [Authorize(Roles = MessagingConstants.RoleAdmin)]
    public async Task<ActionResult<object>> SendAdminInvite(string userId, [FromQuery] string? provider, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
            return Unauthorized();

        var resolved = await ResolveEnabledProviderAsync(provider, ct);
        if (resolved.Error != null)
            return BadRequest(new { result = "NoProviderAvailable", error = resolved.Error });

        var result = await _inviteSendService.SendAsync(userId, adminId, resolved.Provider!.ProviderType, resolved.Provider.ConfigJson, ct);
        return Ok(new { result = result.ToString() });
    }

    /// <summary>
    /// Resolves which enabled messaging provider to use: the explicitly requested one (matched by
    /// Name or ProviderType), or - when none was requested - the sole enabled provider. Zero or
    /// multiple enabled providers without an explicit choice is reported as an error rather than
    /// guessed, mirroring the same auto-resolve rule the send_message skill uses.
    /// </summary>
    private async Task<(MessagingProvider? Provider, string? Error)> ResolveEnabledProviderAsync(string? requestedProvider, CancellationToken ct)
    {
        var enabledProviders = await _providerRepository.GetEnabledAsync();

        if (!string.IsNullOrWhiteSpace(requestedProvider))
        {
            var match = enabledProviders.FirstOrDefault(p =>
                string.Equals(p.Name, requestedProvider, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.ProviderType, requestedProvider, StringComparison.OrdinalIgnoreCase));

            return match != null
                ? (match, null)
                : (null, $"No enabled messaging provider matches '{requestedProvider}'.");
        }

        if (enabledProviders.Count == 0)
            return (null, "No messaging provider is configured and enabled.");

        if (enabledProviders.Count > 1)
        {
            var names = string.Join(", ", enabledProviders.Select(p => p.Name));
            return (null, $"Multiple messaging providers are enabled ({names}). Specify which one to use.");
        }

        return (enabledProviders[0], null);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var contact = await _repository.GetByIdAsync(id, ct);
        if (contact == null)
            return NotFound();

        var isOwner = string.Equals(contact.UserId, userId, StringComparison.Ordinal);
        if (!isOwner && !User.IsInRole(MessagingConstants.RoleAdmin))
        {
            // Not Forbid(): that resolves the default authentication scheme, and AddIdentity moves the
            // default to cookie authentication, whose forbid handler answers with a redirect instead of
            // a 403. The status code is stated outright so the answer cannot depend on scheme order.
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        await _repository.DeleteAsync(id, ct);
        await _unitOfWork.CompleteAsync();

        await PromotePreferredSuccessorAsync(contact, ct);

        return NoContent();
    }

    /// <summary>
    /// Hands the preferred flag to the oldest remaining channel when the preferred one was removed.
    /// Without this the card would show no preferred channel at all while GetPreferredAsync still
    /// falls back to the oldest row and keeps sending there - the display would contradict where a
    /// night-time wake-up call actually goes, on the one point the user is looking at.
    /// Runs after the delete was committed on purpose: the partial unique index permits a single
    /// preferred row per user, and promoting before the old row is soft-deleted would briefly leave
    /// two rows matching it.
    /// </summary>
    private async Task PromotePreferredSuccessorAsync(UserMessengerContact removed, CancellationToken ct)
    {
        if (!removed.IsPreferred)
            return;

        var remaining = await _repository.GetByUserIdAsync(removed.UserId, ct);
        var successor = remaining
            .Where(c => c.Id != removed.Id)
            .OrderBy(c => c.CreateTime)
            .FirstOrDefault();

        if (successor == null)
            return;

        successor.IsPreferred = true;
        await _repository.UpdateAsync(successor, ct);
        await _unitOfWork.CompleteAsync();
    }

    private string? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }

    private static UserMessengerContactDto ToDto(UserMessengerContact contact)
    {
        return new UserMessengerContactDto
        {
            Id = contact.Id,
            UserId = contact.UserId,
            Type = contact.Type,
            Value = contact.Value,
            Description = contact.Description,
            IsPreferred = contact.IsPreferred
        };
    }
}
