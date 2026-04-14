// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Repository interface for TelegramOnboardingToken persistence.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface ITelegramOnboardingTokenRepository
{
    Task AddAsync(TelegramOnboardingToken token, CancellationToken ct = default);

    Task<TelegramOnboardingToken?> GetByTokenAsync(string token, CancellationToken ct = default);

    Task<IReadOnlyList<TelegramOnboardingToken>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);

    Task InvalidateAllForClientAsync(Guid clientId, CancellationToken ct = default);
}
