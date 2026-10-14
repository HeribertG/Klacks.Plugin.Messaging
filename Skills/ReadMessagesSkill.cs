// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Skill for reading recent messages from messaging providers.
/// Used by the chatbot to display recent incoming or outgoing messages. Administrators read the unfiltered store;
/// every other caller reads only through IMessagingAccessScope, the same narrowing as the messages REST route.
/// </summary>
/// <param name="provider">Optional: filter by provider name</param>
/// <param name="direction">Optional: filter by direction (inbound, outbound, all)</param>
/// <param name="count">Number of messages to return (default: 10)</param>
/// <param name="sender">Optional: filter by sender</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("read_messages")]
public class ReadMessagesSkill : BaseSkillImplementation
{
    private readonly IMessageRepository _messageRepository;
    private readonly IMessagingProviderRepository _providerRepository;
    private readonly IMessagingAccessScope _accessScope;

    public ReadMessagesSkill(
        IMessageRepository messageRepository,
        IMessagingProviderRepository providerRepository,
        IMessagingAccessScope accessScope)
    {
        _accessScope = accessScope;
        _messageRepository = messageRepository;
        _providerRepository = providerRepository;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var providerName = GetParameter<string>(parameters, "provider");
        var directionStr = GetParameter<string>(parameters, "direction", "all")!;
        var count = GetParameter<int?>(parameters, "count", 10) ?? 10;
        var sender = GetParameter<string>(parameters, "sender");

        count = Math.Clamp(count, 1, 50);

        Guid? providerId = null;
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            var provider = await _providerRepository.GetByNameAsync(providerName);
            if (provider == null)
                return SkillResult.Error($"Messaging provider '{providerName}' not found.");
            providerId = provider.Id;
        }

        MessageDirection? direction = directionStr.ToLowerInvariant() switch
        {
            "inbound" => MessageDirection.Inbound,
            "outbound" => MessageDirection.Outbound,
            _ => null
        };

        var messages = context.UserPermissions.Contains(MessagingConstants.RoleAdmin)
            ? await _messageRepository.GetMessagesAsync(providerId, direction, sender, scope: null, count, 0)
            : await _accessScope.GetVisibleMessagesAsync(providerId, direction, sender, scope: null, count, 0, cancellationToken);

        var result = messages.Select(m => new
        {
            m.Id,
            Provider = m.Provider?.Name ?? "unknown",
            m.Sender,
            m.SenderDisplayName,
            m.Recipient,
            m.Content,
            Direction = m.Direction.ToString(),
            Status = m.Status.ToString(),
            m.Timestamp
        }).ToList();

        var directionLabel = direction?.ToString() ?? "all";
        return SkillResult.SuccessResult(
            result,
            $"Found {result.Count} {directionLabel} message(s){(providerName != null ? $" from {providerName}" : "")}.");
    }
}
