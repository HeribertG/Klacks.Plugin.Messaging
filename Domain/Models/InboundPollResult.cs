// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Result of one polling round against a provider that does not deliver via webhook.
/// </summary>
/// <param name="Messages">Messages newer than the cursor handed in, oldest first.</param>
/// <param name="NextCursor">Cursor to hand back on the next round, or the previous one when nothing arrived.</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public record InboundPollResult(IReadOnlyList<IncomingMessage> Messages, string? NextCursor);
