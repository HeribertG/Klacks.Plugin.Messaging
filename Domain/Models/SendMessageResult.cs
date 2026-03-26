// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Result model returned after attempting to send a message.
/// </summary>
/// <param name="Success">Whether the message was sent successfully</param>
/// <param name="ExternalMessageId">Provider-assigned message ID on success</param>
/// <param name="ErrorMessage">Error details on failure</param>
namespace Klacks.Plugin.Messaging.Domain.Models;

public record SendMessageResult(
    bool Success,
    string? ExternalMessageId = null,
    string? ErrorMessage = null);
