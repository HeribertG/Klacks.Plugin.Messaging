// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Reads the owner messenger identity list from the APP_OWNER_MESSENGERS jsonb setting.
/// Used by SendMessageSkill to resolve 'mir' / 'me' / 'myself' recipients.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IOwnerMessengerReader
{
    Task<IReadOnlyList<OwnerMessengerEntry>> GetAllAsync(CancellationToken ct = default);

    Task<OwnerMessengerEntry?> GetByTypeAsync(MessengerType type, CancellationToken ct = default);

    Task<string?> GetOwnerDisplayNameAsync(CancellationToken ct = default);
}
