// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Orchestration service for sending and receiving messages across all configured providers.
/// Resolves the appropriate provider adapter, delegates sending/receiving, and persists messages.
/// </summary>
/// <param name="_providerRepository">Repository for messaging provider lookups</param>
/// <param name="_messageRepository">Repository for message persistence</param>
/// <param name="_unitOfWork">Unit of work for committing changes</param>
/// <param name="_adapterFactory">Factory for creating provider-specific adapters</param>
/// <param name="_logger">Logger instance</param>
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class MessagingService : IMessagingService
{
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly MessagingProviderAdapterFactory _adapterFactory;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IMessagingProviderRepository providerRepository,
        IMessageRepository messageRepository,
        IPluginUnitOfWork unitOfWork,
        MessagingProviderAdapterFactory adapterFactory,
        ILogger<MessagingService> logger)
    {
        _providerRepository = providerRepository;
        _messageRepository = messageRepository;
        _unitOfWork = unitOfWork;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendMessageAsync(string providerName, SendMessageRequest request, CancellationToken ct = default)
    {
        var provider = await _providerRepository.GetByNameAsync(providerName);
        if (provider == null)
            return new SendMessageResult(false, ErrorMessage: $"Provider '{providerName}' not found");

        if (!provider.IsEnabled)
            return new SendMessageResult(false, ErrorMessage: $"Provider '{providerName}' is disabled");

        var adapter = _adapterFactory.Create(provider.ProviderType);
        var result = await adapter.SendAsync(request, provider.ConfigJson, ct);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ExternalMessageId = result.ExternalMessageId ?? string.Empty,
            Recipient = request.Recipient,
            Content = request.Content,
            ContentType = request.ContentType,
            MediaUrl = request.MediaUrl,
            Direction = MessageDirection.Outbound,
            Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
            ErrorMessage = result.ErrorMessage,
            Timestamp = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);
        await _unitOfWork.CompleteAsync();

        return result;
    }

    public async Task<Message?> GetMessageAsync(Guid id, CancellationToken ct = default)
    {
        return await _messageRepository.GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        Guid? providerId,
        MessageDirection? direction,
        string? sender,
        int count = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await _messageRepository.GetMessagesAsync(providerId, direction, sender, count, offset);
    }

    public async Task<Message> ProcessIncomingMessageAsync(string providerName, string body, string? signature, CancellationToken ct = default)
    {
        var provider = await _providerRepository.GetByNameAsync(providerName);
        if (provider == null)
            throw new InvalidOperationException($"Provider '{providerName}' not found");

        var adapter = _adapterFactory.Create(provider.ProviderType);

        var validationResult = adapter.ValidateWebhook(body, signature ?? string.Empty, provider.WebhookSecret);
        if (!validationResult.IsValid)
            throw new UnauthorizedAccessException($"Webhook validation failed for provider '{providerName}'");

        var incoming = adapter.ParseWebhookPayload(body);
        if (incoming == null)
            throw new InvalidOperationException($"Failed to parse webhook payload for provider '{providerName}'");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ExternalMessageId = incoming.ExternalMessageId,
            Sender = incoming.Sender,
            SenderDisplayName = incoming.SenderDisplayName,
            Content = incoming.Content,
            ContentType = incoming.ContentType,
            MediaUrl = incoming.MediaUrl,
            Direction = MessageDirection.Inbound,
            Status = MessageStatus.Delivered,
            Timestamp = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("Processed incoming message {MessageId} from provider {Provider}", message.Id, providerName);

        return message;
    }

    public async Task<bool> TestProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
            return false;

        var adapter = _adapterFactory.Create(provider.ProviderType);
        return await adapter.ValidateConfigAsync(provider.ConfigJson, ct);
    }
}
