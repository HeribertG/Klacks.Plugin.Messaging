// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Result of validating an incoming webhook request from a messaging provider.
/// </summary>
/// <param name="IsValid">Whether the webhook signature is valid</param>
/// <param name="ChallengeResponse">Optional challenge response for webhook verification handshakes</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record WebhookValidationResult(
    bool IsValid,
    string? ChallengeResponse = null);
