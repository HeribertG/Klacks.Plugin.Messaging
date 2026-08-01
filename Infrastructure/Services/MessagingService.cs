// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Orchestration service for sending and receiving messages across all configured providers.
/// Resolves the appropriate provider adapter, delegates sending/receiving, and persists messages.
/// </summary>
/// <param name="_providerRepository">Repository for messaging provider lookups</param>
/// <param name="_messageRepository">Repository for message persistence</param>
/// <param name="_unitOfWork">Unit of work for committing changes</param>
/// <param name="_adapterFactory">Factory for creating provider-specific adapters</param>
/// <param name="_clientIdNumberReader">Reader for resolving client GUIDs from integer id numbers</param>
/// <param name="_settingsReader">Reader for the configurable broadcast pacing interval</param>
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
    private readonly IClientIdNumberReader _clientIdNumberReader;
    private readonly IClientPhoneReader _clientPhoneReader;
    private readonly IPluginUnitOfWork _unitOfWork;
    private readonly IPluginSettingsReader _settingsReader;
    private readonly MessagingProviderAdapterFactory _adapterFactory;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IMessagingProviderRepository providerRepository,
        IMessageRepository messageRepository,
        IMessengerContactRepository messengerContactRepository,
        IClientGroupReader clientGroupReader,
        IClientIdNumberReader clientIdNumberReader,
        IClientPhoneReader clientPhoneReader,
        IPluginUnitOfWork unitOfWork,
        IPluginSettingsReader settingsReader,
        MessagingProviderAdapterFactory adapterFactory,
        ILogger<MessagingService> logger)
    {
        _providerRepository = providerRepository;
        _messageRepository = messageRepository;
        _messengerContactRepository = messengerContactRepository;
        _clientGroupReader = clientGroupReader;
        _clientIdNumberReader = clientIdNumberReader;
        _clientPhoneReader = clientPhoneReader;
        _unitOfWork = unitOfWork;
        _settingsReader = settingsReader;
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

    public async Task<WebhookProcessingResult> ProcessIncomingMessageAsync(string providerName, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName);
        if (provider == null)
            throw new InvalidOperationException($"Provider '{providerName}' not found");

        var adapter = _adapterFactory.Create(provider.ProviderType);

        var context = new WebhookValidationContext(body, headers, provider.ConfigJson, provider.WebhookSecret);
        var validationResult = adapter.ValidateWebhook(context);
        if (!validationResult.IsValid)
            throw new UnauthorizedAccessException($"Webhook validation failed for provider '{providerName}'");

        if (validationResult.ChallengeResponse != null)
            return new WebhookProcessingResult(ChallengeResponse: validationResult.ChallengeResponse);

        var incoming = adapter.ParseWebhookPayload(body);
        if (incoming == null)
        {
            _logger.LogDebug("Webhook payload for provider {Provider} contained no processable message", providerName);
            return new WebhookProcessingResult();
        }

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

        return new WebhookProcessingResult(message);
    }

    public async Task<string?> VerifySubscriptionChallengeAsync(string providerName, string? verifyToken, string challenge, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName);
        if (provider == null)
            return null;

        var adapter = _adapterFactory.Create(provider.ProviderType);
        if (adapter is not IWebhookSubscriptionVerifier verifier)
            return null;

        return verifier.VerifySubscription(provider.ConfigJson, verifyToken ?? string.Empty) ? challenge : null;
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

        return await BuildPreviewAsync(provider, adapter, clientIds, ct);
    }

    public async Task<BroadcastSendResult> SendBroadcastAsync(string providerName, Guid groupId, string content, string contentType = "text", CancellationToken ct = default)
    {
        var (provider, adapter) = await ResolveEnabledProviderAsync(providerName, content);
        var clientIds = await _clientGroupReader.GetClientIdsInGroupAsync(groupId, ct);

        if (clientIds.Count == 0)
            throw new InvalidOperationException("Group is empty");

        return await ExecuteBroadcastAsync(providerName, provider, adapter, clientIds, content, contentType, ct);
    }

    public async Task<BroadcastPreview> PreviewBroadcastToIdNumbersAsync(string providerName, IReadOnlyCollection<int> idNumbers, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName)
            ?? throw new InvalidOperationException($"Provider '{providerName}' not found");

        var adapter = _adapterFactory.Create(provider.ProviderType);
        var clientIds = await _clientIdNumberReader.GetClientIdsByIdNumbersAsync(idNumbers, ct);

        if (clientIds.Count == 0)
            throw new InvalidOperationException("No clients found for the given id numbers");

        return await BuildPreviewAsync(provider, adapter, clientIds, ct);
    }

    public async Task<BroadcastSendResult> SendBroadcastToIdNumbersAsync(string providerName, IReadOnlyCollection<int> idNumbers, string content, string contentType = "text", CancellationToken ct = default)
    {
        var (provider, adapter) = await ResolveEnabledProviderAsync(providerName, content);
        var clientIds = await _clientIdNumberReader.GetClientIdsByIdNumbersAsync(idNumbers, ct);

        if (clientIds.Count == 0)
            throw new InvalidOperationException("No clients found for the given id numbers");

        return await ExecuteBroadcastAsync(providerName, provider, adapter, clientIds, content, contentType, ct);
    }

    public async Task<bool> RegisterWebhookAsync(Guid providerId, CancellationToken ct = default)
    {
        var provider = await _providerRepository.GetByIdAsync(providerId);
        if (provider == null)
            return false;

        var adapter = _adapterFactory.Create(provider.ProviderType);
        if (adapter is not IWebhookRegistrar registrar)
            return true;

        return await registrar.RegisterWebhookAsync(provider.ConfigJson, provider.WebhookSecret, ct);
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

    private async Task<(MessagingProvider Provider, IMessagingProviderAdapter Adapter)> ResolveEnabledProviderAsync(string providerName, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Broadcast content must not be empty", nameof(content));

        var provider = await ResolveProviderAsync(providerName)
            ?? throw new InvalidOperationException($"Provider '{providerName}' not found");

        if (!provider.IsEnabled)
            throw new InvalidOperationException($"Provider '{providerName}' is disabled");

        return (provider, _adapterFactory.Create(provider.ProviderType));
    }

    private async Task<BroadcastPreview> BuildPreviewAsync(
        MessagingProvider provider,
        IMessagingProviderAdapter adapter,
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken ct)
    {
        var (recipients, skipped) = await ResolveRecipientsAsync(provider, adapter, clientIds, ct);

        var contactCount = recipients.Count(r => !r.FromPhoneFallback);
        var phoneCount = recipients.Count(r => r.FromPhoneFallback);

        return new BroadcastPreview(clientIds.Count, contactCount, phoneCount, skipped, adapter.SupportsPhoneAsRecipient);
    }

    private async Task<(IReadOnlyList<BroadcastRecipient> Recipients, int Skipped)> ResolveRecipientsAsync(
        MessagingProvider provider,
        IMessagingProviderAdapter adapter,
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken ct)
    {
        if (!Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var messengerType))
            throw new InvalidOperationException($"Cannot map provider type '{provider.ProviderType}' to MessengerType");

        IReadOnlyDictionary<Guid, string?> phoneFallbacks = adapter.SupportsPhoneAsRecipient
            ? await _clientPhoneReader.GetMobilePhonesAsync(clientIds, ct)
            : new Dictionary<Guid, string?>();

        var recipients = new List<BroadcastRecipient>();
        var skipped = 0;

        foreach (var clientId in clientIds)
        {
            var contact = await _messengerContactRepository.GetByClientAndTypeAsync(clientId, messengerType, ct);
            if (contact != null && !string.IsNullOrWhiteSpace(contact.Value))
            {
                recipients.Add(new BroadcastRecipient(clientId, contact.Value, contact.Description, FromPhoneFallback: false));
                continue;
            }

            if (adapter.SupportsPhoneAsRecipient
                && phoneFallbacks.TryGetValue(clientId, out var phone)
                && !string.IsNullOrWhiteSpace(phone))
            {
                recipients.Add(new BroadcastRecipient(clientId, phone!, null, FromPhoneFallback: true));
                continue;
            }

            skipped++;
        }

        return (recipients, skipped);
    }

    private async Task<BroadcastSendResult> ExecuteBroadcastAsync(
        string providerName,
        MessagingProvider provider,
        IMessagingProviderAdapter adapter,
        IReadOnlyCollection<Guid> clientIds,
        string content,
        string contentType,
        CancellationToken ct)
    {
        var (recipients, skipped) = await ResolveRecipientsAsync(provider, adapter, clientIds, ct);

        if (recipients.Count == 0)
            throw new InvalidOperationException($"No recipients with messenger contact or phone fallback for provider '{providerName}'");

        var pacingDelay = await ResolvePacingDelayAsync();
        var deadline = DateTime.UtcNow.AddSeconds(MessagingRateLimitConstants.MaxBroadcastDurationSeconds);
        var broadcastId = Guid.NewGuid();
        var sent = 0;
        var failed = 0;
        var throttled = 0;
        var abandoned = 0;

        for (var index = 0; index < recipients.Count; index++)
        {
            var recipient = recipients[index];

            if (DateTime.UtcNow >= deadline || ct.IsCancellationRequested)
            {
                await _messageRepository.AddAsync(BuildBroadcastMessage(
                    provider,
                    recipient,
                    new SendMessageResult(false, ErrorMessage: MessagingRateLimitConstants.BroadcastBudgetExceededError),
                    content,
                    contentType,
                    broadcastId));

                failed++;
                abandoned++;
                continue;
            }

            var result = await TrySendAsync(provider, adapter, recipient, content, contentType, broadcastId, ct);

            await _messageRepository.AddAsync(BuildBroadcastMessage(provider, recipient, result, content, contentType, broadcastId));

            if (result.Success)
                sent++;
            else
                failed++;

            if (result.IsThrottled)
                throttled++;

            if (index < recipients.Count - 1)
                await PaceAsync(pacingDelay, ct);
        }

        await _unitOfWork.CompleteAsync();

        if (abandoned > 0)
        {
            _logger.LogWarning(
                "Broadcast {BroadcastId} via {Provider} stopped early: {Abandoned} of {Total} recipients were never attempted",
                broadcastId, providerName, abandoned, recipients.Count);
        }

        _logger.LogInformation(
            "Broadcast {BroadcastId} via {Provider}: total={Total} sent={Sent} failed={Failed} skipped={Skipped} throttled={Throttled}",
            broadcastId, providerName, clientIds.Count, sent, failed, skipped, throttled);

        return new BroadcastSendResult(broadcastId, clientIds.Count, sent, failed, skipped);
    }

    private async Task<SendMessageResult> TrySendAsync(
        MessagingProvider provider,
        IMessagingProviderAdapter adapter,
        BroadcastRecipient recipient,
        string content,
        string contentType,
        Guid broadcastId,
        CancellationToken ct)
    {
        try
        {
            return await adapter.SendAsync(new SendMessageRequest(recipient.Recipient, content, contentType), provider.ConfigJson, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Broadcast {BroadcastId} send failed for client {ClientId}", broadcastId, recipient.ClientId);
            return new SendMessageResult(false, ErrorMessage: ex.Message);
        }
    }

    private Message BuildBroadcastMessage(
        MessagingProvider provider,
        BroadcastRecipient recipient,
        SendMessageResult result,
        string content,
        string contentType,
        Guid broadcastId)
    {
        return new Message
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ClientId = recipient.ClientId,
            BroadcastId = broadcastId,
            ExternalMessageId = result.ExternalMessageId ?? string.Empty,
            Recipient = recipient.Recipient,
            RecipientDisplayName = recipient.DisplayName ?? string.Empty,
            Content = content,
            ContentType = contentType,
            Direction = MessageDirection.Outbound,
            Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
            ErrorMessage = result.ErrorMessage,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Smooths bursts between sends. A cancelled wait is swallowed so the loop reaches its next
    /// iteration, records the remaining recipients as not attempted and still commits what was
    /// already sent, instead of unwinding and losing the log of every delivered message.
    /// </summary>
    private static async Task PaceAsync(int pacingDelay, CancellationToken ct)
    {
        if (pacingDelay <= 0)
            return;

        try
        {
            await Task.Delay(pacingDelay, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<int> ResolvePacingDelayAsync()
    {
        var configured = await _settingsReader.GetSettingAsync(MessagingRateLimitConstants.SettingBroadcastPacingMs);

        if (!int.TryParse(configured, out var pacing) || pacing < 0)
            return MessagingRateLimitConstants.DefaultBroadcastPacingMilliseconds;

        return Math.Min(pacing, MessagingRateLimitConstants.MaxBroadcastPacingMilliseconds);
    }
}
