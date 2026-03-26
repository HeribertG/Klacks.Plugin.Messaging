// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Skill for reading recent messages from messaging providers.
/// Used by the chatbot to display recent incoming or outgoing messages.
/// </summary>
/// <param name="provider">Optional: filter by provider name</param>
/// <param name="direction">Optional: filter by direction (inbound, outbound, all)</param>
/// <param name="count">Number of messages to return (default: 10)</param>
/// <param name="sender">Optional: filter by sender</param>

using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("read_messages")]
public class ReadMessagesSkill : BaseSkillImplementation
{
    private readonly IMessageRepository _messageRepository;
    private readonly IMessagingProviderRepository _providerRepository;

    public ReadMessagesSkill(
        IMessageRepository messageRepository,
        IMessagingProviderRepository providerRepository)
    {
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

        var messages = await _messageRepository.GetMessagesAsync(providerId, direction, sender, count, 0);

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
