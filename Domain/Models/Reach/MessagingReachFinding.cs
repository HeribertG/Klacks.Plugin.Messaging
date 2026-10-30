// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// One deterministic finding of the reach advice.
/// </summary>
/// <param name="Code">Finding code (MessagingReachFindingCodes)</param>
/// <param name="Count">People the finding is about</param>
/// <param name="Provider">The provider it names, when it names one</param>

namespace Klacks.Plugin.Messaging.Domain.Models.Reach;

public sealed record MessagingReachFinding(string Code, int Count, string? Provider = null);
