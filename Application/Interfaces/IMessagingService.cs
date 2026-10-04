// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Orchestration service for sending and receiving messages across all configured providers.
/// </summary>
/// <param name="providerName">Internal name of the messaging provider</param>
/// <param name="request">The outbound message to send</param>
/// <param name="id">Unique identifier of the message</param>
/// <param name="direction">Optional filter by message direction</param>
/// <param name="sender">Optional filter by sender address</param>
/// <param name="scope">Optional filter by client-vs-internal scope</param>
/// <param name="count">Number of messages to return</param>
/// <param name="offset">Number of messages to skip</param>
/// <param name="body">Raw webhook request body</param>
/// <param name="headers">HTTP request headers of the webhook call</param>
/// <param name="verifyToken">Verify token sent by the external platform during subscription</param>
/// <param name="challenge">Challenge string to echo back on successful subscription verification</param>
/// <param name="providerId">Unique identifier of the provider to test or register webhook for</param>
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessagingService
{
    Task<SendMessageResult> SendMessageAsync(string providerName, SendMessageRequest request, CancellationToken ct = default);

    Task<Message?> GetMessageAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid? providerId, MessageDirection? direction, string? sender, MessageScope? scope = null, int count = 20, int offset = 0, CancellationToken ct = default);

    Task<WebhookProcessingResult> ProcessIncomingMessageAsync(string providerName, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);

    /// <summary>
    /// Authenticates a webhook request exactly like ProcessIncomingMessageAsync does - provider resolved by
    /// name, then by the first enabled provider of that type; an unknown or disabled provider is rejected -
    /// without parsing or persisting anything. For paths that act on the payload themselves, such as the
    /// Telegram /start onboarding command. Records a webhook hit or a signature rejection.
    /// </summary>
    Task<bool> AuthenticateWebhookAsync(string providerName, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);

    /// <summary>
    /// Persists one already-parsed inbound message. Shared by the webhook route and the poller so
    /// both produce identical rows. Returns null when the provider is unknown or the message was
    /// already stored - a poll cursor that slips would otherwise replay messages, and each replay
    /// costs an LLM turn plus an outbound reply.
    /// </summary>
    Task<Message?> IngestInboundMessageAsync(string providerName, IncomingMessage incoming, CancellationToken ct = default);

    Task<string?> VerifySubscriptionChallengeAsync(string providerName, string? verifyToken, string challenge, CancellationToken ct = default);

    Task<bool> TestProviderAsync(Guid providerId, CancellationToken ct = default);

    Task<BroadcastPreview> PreviewBroadcastAsync(string providerName, Guid groupId, CancellationToken ct = default);

    Task<BroadcastSendResult> SendBroadcastAsync(string providerName, Guid groupId, string content, string contentType = "text", CancellationToken ct = default);

    /// <summary>
    /// Previews a broadcast to an explicit client list, answered like an empty group when the list is empty.
    /// Lets a caller narrow the audience (e.g. to the clients it may see) before anything is resolved.
    /// </summary>
    Task<BroadcastPreview> PreviewBroadcastToClientsAsync(string providerName, IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default);

    /// <summary>
    /// Sends a broadcast to an explicit client list. An empty list fails with emptyAudienceError, so a narrowed
    /// audience answers exactly like the unnarrowed route would for an empty group or unknown id numbers.
    /// </summary>
    Task<BroadcastSendResult> SendBroadcastToClientsAsync(string providerName, IReadOnlyCollection<Guid> clientIds, string content, string contentType, string emptyAudienceError, CancellationToken ct = default);

    /// <summary>
    /// The client a recipient address belongs to, resolved with the same provider lookup and messenger-contact
    /// match the send path uses to attribute the outbound message; null when no contact matches.
    /// </summary>
    Task<Guid?> ResolveRecipientClientIdAsync(string providerName, string recipient, CancellationToken ct = default);

    Task<BroadcastPreview> PreviewBroadcastToIdNumbersAsync(string providerName, IReadOnlyCollection<int> idNumbers, CancellationToken ct = default);

    Task<BroadcastSendResult> SendBroadcastToIdNumbersAsync(string providerName, IReadOnlyCollection<int> idNumbers, string content, string contentType = "text", CancellationToken ct = default);

    Task<bool> RegisterWebhookAsync(Guid providerId, CancellationToken ct = default);
}
