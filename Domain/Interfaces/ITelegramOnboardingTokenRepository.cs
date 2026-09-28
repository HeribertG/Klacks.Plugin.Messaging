// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

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
