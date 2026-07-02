// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Outcome of processing an incoming webhook request.
/// </summary>
/// <param name="Message">The persisted inbound message, or null if the payload contained no relevant message</param>
/// <param name="ChallengeResponse">Challenge string to echo back for webhook verification handshakes</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record WebhookProcessingResult(
    Message? Message = null,
    string? ChallengeResponse = null);
