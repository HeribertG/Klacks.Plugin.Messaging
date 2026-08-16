// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// REST API for the messenger channels of the signed-in application user: list them, ask for a
/// pairing code, and drop one again. Distinct from MessengerContactController, which manages the
/// channels of a Client - an employee - and is operated by a planner on somebody else's behalf.
/// Every route here derives the user from the access token instead of taking a user id, so the
/// pairing code route has no way to name a foreign account. Only the two read/delete routes an
/// administrator legitimately needs across accounts accept a user id, and they are role-gated.
/// </summary>
/// <param name="repository">Reads and soft-deletes the user-level messenger contacts.</param>
/// <param name="pairingService">Issues the short-lived pairing codes.</param>
/// <param name="unitOfWork">Commits the delete.</param>

using System.Security.Claims;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Filters;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.DTOs;
using Klacks.Plugin.Messaging.Application.Interfaces;
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

    public UserMessengerContactController(
        IUserMessengerContactRepository repository,
        IUserMessengerPairingService pairingService,
        IPluginUnitOfWork unitOfWork)
    {
        _repository = repository;
        _pairingService = pairingService;
        _unitOfWork = unitOfWork;
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
    /// the only account a code can ever be issued for is the one the token authenticated.
    /// </summary>
    [HttpPost("pairing-code")]
    public async Task<ActionResult<UserMessengerPairingCodeDto>> CreatePairingCode(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var issued = await _pairingService.IssueCodeAsync(userId, ct);

        return Ok(new UserMessengerPairingCodeDto
        {
            Code = issued.Code,
            ExpiresAt = issued.ExpiresAt
        });
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
