// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Orchestration service for sending and receiving messages across all configured providers.
/// </summary>
/// <param name="providerName">Internal name of the messaging provider</param>
/// <param name="request">The outbound message to send</param>
/// <param name="id">Unique identifier of the message</param>
/// <param name="providerId">Optional filter by provider</param>
/// <param name="direction">Optional filter by message direction</param>
/// <param name="sender">Optional filter by sender address</param>
/// <param name="count">Number of messages to return</param>
/// <param name="offset">Number of messages to skip</param>
/// <param name="body">Raw webhook request body</param>
/// <param name="signature">Webhook signature header</param>
/// <param name="providerId">Unique identifier of the provider to test</param>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessagingService
{
    Task<SendMessageResult> SendMessageAsync(string providerName, SendMessageRequest request, CancellationToken ct = default);

    Task<Message?> GetMessageAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid? providerId, MessageDirection? direction, string? sender, int count = 20, int offset = 0, CancellationToken ct = default);

    Task<Message> ProcessIncomingMessageAsync(string providerName, string body, string? signature, CancellationToken ct = default);

    Task<bool> TestProviderAsync(Guid providerId, CancellationToken ct = default);

    Task<BroadcastPreview> PreviewBroadcastAsync(string providerName, Guid groupId, CancellationToken ct = default);

    Task<BroadcastSendResult> SendBroadcastAsync(string providerName, Guid groupId, string content, string contentType = "text", CancellationToken ct = default);
}
