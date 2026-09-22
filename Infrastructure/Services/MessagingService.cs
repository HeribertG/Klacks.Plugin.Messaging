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
/// <param name="_ownerMessengerReader">Second source of known inbound senders, next to MessengerContact</param>
/// <param name="_userMessengerContactRepository">Third source of known inbound senders: the application users who paired a channel</param>
/// <param name="_appUserDirectoryReader">Resolves a paired application user's real name for display, given the matched UserMessengerContact</param>
/// <param name="_inboundObservers">Anyone in the host who wants to hear that a known user answered; empty is a valid state</param>
/// <param name="_clientMessengerObservers">Anyone in the host who wants to hear that a known client sent a message; empty is a valid state</param>
/// <param name="_logSuppressionCache">Keeps a discarded sender from being logged on every message</param>
/// <param name="_logger">Logger instance</param>
using System.Globalization;
using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Klacks.Plugin.Messaging.Infrastructure.Services;

public class MessagingService : IMessagingService
{
    private const string UnknownSenderLogKeyPrefix = "messaging:unknown-sender:";
    private const char CacheKeySeparator = '|';

    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessengerContactRepository _messengerContactRepository;
    private readonly IOwnerMessengerReader _ownerMessengerReader;
    private readonly IUserMessengerContactRepository _userMessengerContactRepository;
    private readonly IAppUserDirectoryReader _appUserDirectoryReader;
    private readonly IEnumerable<IInboundMessengerObserver> _inboundObservers;
    private readonly IEnumerable<IInboundClientMessengerObserver> _clientMessengerObservers;
    private readonly IMemoryCache _logSuppressionCache;
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
        IOwnerMessengerReader ownerMessengerReader,
        IUserMessengerContactRepository userMessengerContactRepository,
        IAppUserDirectoryReader appUserDirectoryReader,
        IEnumerable<IInboundMessengerObserver> inboundObservers,
        IEnumerable<IInboundClientMessengerObserver> clientMessengerObservers,
        IMemoryCache logSuppressionCache,
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
        _ownerMessengerReader = ownerMessengerReader;
        _userMessengerContactRepository = userMessengerContactRepository;
        _appUserDirectoryReader = appUserDirectoryReader;
        _inboundObservers = inboundObservers;
        _clientMessengerObservers = clientMessengerObservers;
        _logSuppressionCache = logSuppressionCache;
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

        if (request.Actions is { Count: > 0 } && !adapter.SupportsStructuredActions)
        {
            return await PersistOutboundAsync(provider, request, new SendMessageResult(
                false,
                ErrorMessage: string.Format(
                    CultureInfo.InvariantCulture,
                    MessagingConstants.StructuredActionsUnsupportedErrorFormat,
                    providerName)), ct);
        }

        var result = await adapter.SendAsync(request, provider.ConfigJson, ct);

        return await PersistOutboundAsync(provider, request, result, ct);
    }

    /// <summary>
    /// Stores the outcome of an outbound attempt. A refused send is recorded here rather than
    /// returned early on purpose: structured actions exist to produce an exact record, and an early
    /// return would leave none of the attempt behind. ClientId is resolved the same way as on the
    /// inbound side (MessengerContact lookup by type+value) so outbound client replies classify as
    /// scope=Client instead of falling into internal by default.
    /// </summary>
    private async Task<SendMessageResult> PersistOutboundAsync(
        MessagingProvider provider,
        SendMessageRequest request,
        SendMessageResult result,
        CancellationToken ct)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ClientId = await ResolveOutboundClientIdAsync(provider, request.Recipient, ct),
            ExternalMessageId = result.ExternalMessageId ?? string.Empty,
            SenderDisplayName = request.SenderDisplayName ?? string.Empty,
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

    private async Task<Guid?> ResolveOutboundClientIdAsync(MessagingProvider provider, string recipient, CancellationToken ct)
    {
        if (!Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var messengerType))
            return null;

        var contact = await _messengerContactRepository.GetByTypeAndValueAsync(messengerType, recipient, ct);
        return contact?.ClientId;
    }

    public async Task<Message?> GetMessageAsync(Guid id, CancellationToken ct = default)
    {
        return await _messageRepository.GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        Guid? providerId,
        MessageDirection? direction,
        string? sender,
        MessageScope? scope = null,
        int count = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, MessagingConstants.MinMessageQueryCount, MessagingConstants.MaxMessageQueryCount);
        offset = Math.Max(offset, 0);
        return await _messageRepository.GetMessagesAsync(providerId, direction, sender, scope, count, offset);
    }

    public async Task<WebhookProcessingResult> ProcessIncomingMessageAsync(string providerName, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName);
        if (provider == null)
            throw new InvalidOperationException($"Provider '{providerName}' not found");

        if (!provider.IsEnabled)
        {
            _logger.LogWarning("Rejected incoming webhook for disabled provider '{Provider}'", providerName);
            throw new UnauthorizedAccessException($"Provider '{providerName}' is disabled");
        }

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

        var message = await PersistInboundAsync(provider, incoming, ct);
        return message == null ? new WebhookProcessingResult() : new WebhookProcessingResult(message);
    }

    public async Task<Message?> IngestInboundMessageAsync(string providerName, IncomingMessage incoming, CancellationToken ct = default)
    {
        var provider = await ResolveProviderAsync(providerName);
        if (provider == null)
        {
            _logger.LogWarning("Cannot ingest inbound message: provider '{Provider}' not found", providerName);
            return null;
        }

        return await PersistInboundAsync(provider, incoming, ct);
    }

    private async Task<Message?> PersistInboundAsync(MessagingProvider provider, IncomingMessage incoming, CancellationToken ct)
    {
        if (await _messageRepository.InboundExistsAsync(provider.Id, incoming.ExternalMessageId, ct))
        {
            _logger.LogDebug(
                "Skipping already stored inbound message {ExternalId} from provider {Provider}",
                incoming.ExternalMessageId,
                provider.Name);
            return null;
        }

        if (!Enum.TryParse<MessengerType>(provider.ProviderType, ignoreCase: true, out var messengerType))
        {
            _logger.LogWarning(
                "Cannot map provider type '{ProviderType}' to MessengerType; discarding inbound message from provider {Provider}",
                provider.ProviderType,
                provider.Name);
            return null;
        }

        var contact = await _messengerContactRepository.GetByTypeAndValueAsync(messengerType, incoming.Sender, ct);
        UserMessengerContact? userContact = null;
        var senderDisplayName = incoming.SenderDisplayName;

        if (contact == null)
        {
            var isOwner = await IsKnownSenderAsync(messengerType, incoming.Sender, ct);
            userContact = await _userMessengerContactRepository.GetByTypeAndValueAsync(messengerType, incoming.Sender, ct);

            if (!isOwner && userContact == null)
            {
                LogUnknownSenderOnce(provider, incoming.Sender);
                return null;
            }

            senderDisplayName = await ResolveInternalSenderDisplayNameAsync(isOwner, userContact, ct) ?? incoming.SenderDisplayName;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ClientId = contact?.ClientId,
            ExternalMessageId = incoming.ExternalMessageId,
            Sender = incoming.Sender,
            SenderDisplayName = senderDisplayName,
            Content = incoming.Content,
            ContentType = incoming.ContentType,
            MediaUrl = incoming.MediaUrl,
            Direction = MessageDirection.Inbound,
            Status = MessageStatus.Delivered,
            Timestamp = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("Processed incoming message {MessageId} from provider {Provider}", message.Id, provider.Name);

        await NotifyInboundObserversAsync(message, messengerType, userContact, ct);

        if (contact != null)
        {
            await NotifyClientMessengerObserversAsync(message, messengerType, ct);
        }

        return message;
    }

    /// <summary>
    /// Tells the host that a message from a paired application user arrived. Stored is not the same
    /// as heard: without this the reply of a planner would sit in the messages table and reach nobody.
    /// Runs after the commit, so an observer always sees a message that really exists, and every
    /// failure is swallowed - an observer that throws must not undo an inbound message that was
    /// already persisted, and must not stop the remaining observers either.
    /// </summary>
    private async Task NotifyInboundObserversAsync(
        Message message,
        MessengerType messengerType,
        UserMessengerContact? userContact,
        CancellationToken ct)
    {
        if (userContact == null)
            return;

        var notification = new InboundMessengerMessage(
            message.Id,
            userContact.UserId,
            messengerType.ToString(),
            message.Sender,
            message.SenderDisplayName,
            message.Content,
            message.Timestamp);

        foreach (var observer in _inboundObservers)
        {
            try
            {
                await observer.OnInboundMessageAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Inbound message observer {Observer} failed for message {MessageId}",
                    observer.GetType().Name,
                    message.Id);
            }
        }
    }

    /// <summary>
    /// Tells the host that a message from a known CLIENT arrived. Parallel to
    /// NotifyInboundObserversAsync but never overlapping with it: a message resolves to a
    /// MessengerContact (client) or a UserMessengerContact (app user), never both, so exactly one of
    /// the two notification paths fires per inbound message. Same error-handling shape as its
    /// user-side counterpart: runs after the commit, and a throwing observer is logged and skipped
    /// rather than allowed to undo the already-persisted message or block the remaining observers.
    /// </summary>
    private async Task NotifyClientMessengerObserversAsync(Message message, MessengerType messengerType, CancellationToken ct)
    {
        var notification = new InboundClientMessengerMessage(
            message.Id,
            message.ClientId!.Value,
            messengerType.ToString(),
            message.Sender,
            message.SenderDisplayName,
            message.Content,
            message.Timestamp);

        foreach (var observer in _clientMessengerObservers)
        {
            try
            {
                await observer.OnInboundMessageAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Client messenger observer {Observer} failed for message {MessageId}",
                    observer.GetType().Name,
                    message.Id);
            }
        }
    }

    /// <summary>
    /// Second source of known senders, consulted only when no MessengerContact matched. The owner's
    /// own messenger identity lives in the APP_OWNER_MESSENGERS setting and deliberately has no
    /// MessengerContact row: that table hangs off a non-nullable ClientId and the owner need not be a
    /// client at all. Discarding unknown senders without this check would silently and permanently
    /// kill the Slack owner bridge, which answers exactly these stored inbound messages.
    /// The third source, UserMessengerContact, is consulted by the caller rather than folded in here,
    /// because a planner replying to an escalation is not only known through that table, they are
    /// known BY it: the caller needs the matched row itself to tell the host who answered, and a
    /// method returning bool would force the same lookup to run twice.
    /// </summary>
    private async Task<bool> IsKnownSenderAsync(MessengerType messengerType, string sender, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sender))
            return false;

        var ownerEntries = await _ownerMessengerReader.GetAllAsync(ct);

        return ownerEntries.Any(entry =>
            entry.Type == messengerType
            && string.Equals(entry.Value, sender, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a real display name for a sender that has no MessengerContact (owner-bridge or
    /// paired application user), so the raw provider identifier (e.g. a Slack user ID) never has to
    /// be shown as the sender in the messaging UI. Owner takes priority since an owner entry and a
    /// paired UserMessengerContact could theoretically both match the same identifier. Returns null,
    /// not the raw identifier, when nothing resolves, so the caller can fall back to whatever the
    /// provider itself reported.
    /// </summary>
    private async Task<string?> ResolveInternalSenderDisplayNameAsync(bool isOwner, UserMessengerContact? userContact, CancellationToken ct)
    {
        if (isOwner)
        {
            var ownerName = await _ownerMessengerReader.GetOwnerDisplayNameAsync(ct);
            if (!string.IsNullOrWhiteSpace(ownerName))
                return ownerName;
        }

        if (userContact != null)
        {
            var user = await _appUserDirectoryReader.GetUserAsync(userContact.UserId, ct);
            var name = $"{user?.FirstName} {user?.LastName}".Trim();
            if (name.Length > 0)
                return name;
        }

        return null;
    }

    /// <summary>
    /// Logs a discarded sender once per provider and sender within a suppression window rather than
    /// on every message, so a bot writing continuously cannot fill the log by itself. Deliberately
    /// not once-ever: a recurring rejection has to stay diagnosable, and a process restart would
    /// clear a once-ever marker anyway.
    /// </summary>
    private void LogUnknownSenderOnce(MessagingProvider provider, string sender)
    {
        var cacheKey = UnknownSenderLogKeyPrefix + provider.Id + CacheKeySeparator + sender;
        if (_logSuppressionCache.TryGetValue(cacheKey, out _))
            return;

        _logSuppressionCache.Set(
            cacheKey,
            true,
            TimeSpan.FromMinutes(MessagingConstants.UnknownSenderLogSuppressionMinutes));

        _logger.LogWarning(
            "Discarded inbound message from unknown sender {Sender} on provider {Provider}: no messenger contact, no paired user channel and not an owner messenger identity",
            sender,
            provider.Name);
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
