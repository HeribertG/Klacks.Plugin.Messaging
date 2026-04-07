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
    private readonly IMessengerContactRepository _messengerContactRepository;
    private readonly IClientGroupReader _clientGroupReader;
    private readonly IClientPhoneReader _clientPhoneReader;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly MessagingProviderAdapterFactory _adapterFactory;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IMessagingProviderRepository providerRepository,
        IMessageRepository messageRepository,
        IMessengerContactRepository messengerContactRepository,
        IClientGroupReader clientGroupReader,
        IClientPhoneReader clientPhoneReader,
        IPluginUnitOfWork unitOfWork,
        MessagingProviderAdapterFactory adapterFactory,
        ILogger<MessagingService> logger)
    {
        _providerRepository = providerRepository;
        _messageRepository = messageRepository;
        _messengerContactRepository = messengerContactRepository;
        _clientGroupReader = clientGroupReader;
        _clientPhoneReader = clientPhoneReader;
        _unitOfWork = unitOfWork;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public async Task<SendMessageResult> SendMessageAsync(string providerName, SendMessageRequest request, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName);
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
        var provider = await ResolveProviderAsync(providerName);
        if (provider == null)
            throw new InvalidOperationException($"Provider '{providerName}' not found");

        var adapter = _adapterFactory.Create(provider.ProviderType);

        var validationResult = adapter.ValidateWebhook(body, signature ?? string.Empty, provider.WebhookSecret);
        if (!validationResult.IsValid)
            throw new UnauthorizedAccessException($"Webhook validation failed for provider '{providerName}'");

        var incoming = adapter.ParseWebhookPayload(body);
        if (incoming == null)
            throw new InvalidOperationException($"Failed to parse webhook payload for provider '{providerName}'");

        Guid? clientId = null;
        if (Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var messengerType))
        {
            var contact = await _messengerContactRepository.GetByTypeAndValueAsync(messengerType, incoming.Sender, ct);
            clientId = contact?.ClientId;
        }
        else
        {
            _logger.LogWarning("Cannot map provider type '{ProviderType}' to MessengerType for inbound contact lookup", provider.ProviderType);
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ClientId = clientId,
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

    public async Task<BroadcastPreview> PreviewBroadcastAsync(string providerName, Guid groupId, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName)
            ?? throw new InvalidOperationException($"Provider '{providerName}' not found");

        var adapter = _adapterFactory.Create(provider.ProviderType);
        var clientIds = await _clientGroupReader.GetClientIdsInGroupAsync(groupId, ct);

        if (clientIds.Count == 0)
        {
            return new BroadcastPreview(0, 0, 0, 0, adapter.SupportsPhoneAsRecipient);
        }

        if (!Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var messengerType))
        {
            throw new InvalidOperationException($"Cannot map provider type '{provider.ProviderType}' to MessengerType");
        }

        var contactCount = 0;
        var phoneCount = 0;
        var skipped = 0;

        IReadOnlyDictionary<Guid, string?> phoneFallbacks = adapter.SupportsPhoneAsRecipient
            ? await _clientPhoneReader.GetMobilePhonesAsync(clientIds, ct)
            : new Dictionary<Guid, string?>();

        foreach (var clientId in clientIds)
        {
            var contact = await _messengerContactRepository.GetByClientAndTypeAsync(clientId, messengerType, ct);
            if (contact != null && !string.IsNullOrWhiteSpace(contact.Value))
            {
                contactCount++;
                continue;
            }

            if (adapter.SupportsPhoneAsRecipient
                && phoneFallbacks.TryGetValue(clientId, out var phone)
                && !string.IsNullOrWhiteSpace(phone))
            {
                phoneCount++;
                continue;
            }

            skipped++;
        }

        return new BroadcastPreview(clientIds.Count, contactCount, phoneCount, skipped, adapter.SupportsPhoneAsRecipient);
    }

    public async Task<BroadcastSendResult> SendBroadcastAsync(string providerName, Guid groupId, string content, string contentType = "text", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Broadcast content must not be empty", nameof(content));

        var provider = await ResolveProviderAsync(providerName)
            ?? throw new InvalidOperationException($"Provider '{providerName}' not found");

        if (!provider.IsEnabled)
            throw new InvalidOperationException($"Provider '{providerName}' is disabled");

        var adapter = _adapterFactory.Create(provider.ProviderType);
        var clientIds = await _clientGroupReader.GetClientIdsInGroupAsync(groupId, ct);

        if (clientIds.Count == 0)
            throw new InvalidOperationException("Group is empty");

        if (!Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var messengerType))
            throw new InvalidOperationException($"Cannot map provider type '{provider.ProviderType}' to MessengerType");

        IReadOnlyDictionary<Guid, string?> phoneFallbacks = adapter.SupportsPhoneAsRecipient
            ? await _clientPhoneReader.GetMobilePhonesAsync(clientIds, ct)
            : new Dictionary<Guid, string?>();

        var recipients = new List<(Guid clientId, string recipient, string? displayName)>();
        var skipped = 0;

        foreach (var clientId in clientIds)
        {
            var contact = await _messengerContactRepository.GetByClientAndTypeAsync(clientId, messengerType, ct);
            if (contact != null && !string.IsNullOrWhiteSpace(contact.Value))
            {
                recipients.Add((clientId, contact.Value, contact.Description));
                continue;
            }

            if (adapter.SupportsPhoneAsRecipient
                && phoneFallbacks.TryGetValue(clientId, out var phone)
                && !string.IsNullOrWhiteSpace(phone))
            {
                recipients.Add((clientId, phone!, null));
                continue;
            }

            skipped++;
        }

        if (recipients.Count == 0)
            throw new InvalidOperationException($"No recipients with messenger contact or phone fallback for provider '{providerName}'");

        var broadcastId = Guid.NewGuid();
        var sent = 0;
        var failed = 0;

        foreach (var (clientId, recipient, displayName) in recipients)
        {
            var request = new SendMessageRequest(recipient, content, contentType);
            SendMessageResult result;
            try
            {
                result = await adapter.SendAsync(request, provider.ConfigJson, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcast {BroadcastId} send failed for client {ClientId}", broadcastId, clientId);
                result = new SendMessageResult(false, ErrorMessage: ex.Message);
            }

            var message = new Message
            {
                Id = Guid.NewGuid(),
                ProviderId = provider.Id,
                ClientId = clientId,
                BroadcastId = broadcastId,
                ExternalMessageId = result.ExternalMessageId ?? string.Empty,
                Recipient = recipient,
                RecipientDisplayName = displayName ?? string.Empty,
                Content = content,
                ContentType = contentType,
                Direction = MessageDirection.Outbound,
                Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
                ErrorMessage = result.ErrorMessage,
                Timestamp = DateTime.UtcNow,
            };

            await _messageRepository.AddAsync(message);

            if (result.Success) sent++; else failed++;
        }

        await _unitOfWork.CompleteAsync();

        _logger.LogInformation(
            "Broadcast {BroadcastId} via {Provider}: total={Total} sent={Sent} failed={Failed} skipped={Skipped}",
            broadcastId, providerName, clientIds.Count, sent, failed, skipped);

        return new BroadcastSendResult(broadcastId, clientIds.Count, sent, failed, skipped);
    }

    private async Task<MessagingProvider?> ResolveProviderAsync(string nameOrType)
    {
        var byName = await _providerRepository.GetByNameAsync(nameOrType);
        if (byName != null)
            return byName;

        var enabled = await _providerRepository.GetEnabledAsync();
        return enabled.FirstOrDefault(p =>
            string.Equals(p.ProviderType, nameOrType, StringComparison.OrdinalIgnoreCase));
    }
}
