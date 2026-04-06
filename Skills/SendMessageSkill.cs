// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Skill for sending messages via a configured messaging provider.
/// Resolves recipient by client name lookup when no phone number is provided.
/// </summary>
/// <param name="provider">The messaging provider name (e.g., "telegram", "sms-twilio")</param>
/// <param name="recipient">Client name or phone number</param>
/// <param name="content">Message text content</param>
/// <param name="contentType">Content type: text, image, document (default: text)</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Klacks.Plugin.Messaging.Skills;

[SkillImplementation("send_message")]
public class SendMessageSkill : BaseSkillImplementation
{
    private static readonly HashSet<string> SelfAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "mir", "ich", "me", "myself", "self"
    };

    private static readonly Dictionary<string, string> ProviderToOwnerSettingKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["telegram"] = "APP_ADDRESS_TELEGRAM",
        ["whatsapp"] = "APP_ADDRESS_WHATSAPP",
        ["signal"] = "APP_ADDRESS_SIGNAL",
        ["sms"] = "APP_ADDRESS_PHONE"
    };

    private readonly IMessagingService _messagingService;
    private readonly DbContext _dbContext;
    private readonly IPluginSettingsReader _settingsReader;

    public SendMessageSkill(
        IMessagingService messagingService,
        DbContext dbContext,
        IPluginSettingsReader settingsReader)
    {
        _messagingService = messagingService;
        _dbContext = dbContext;
        _settingsReader = settingsReader;
    }

    public override async Task<SkillResult> ExecuteAsync(
        SkillExecutionContext context,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var provider = GetRequiredString(parameters, "provider");
        var recipient = GetRequiredString(parameters, "recipient");
        var content = GetRequiredString(parameters, "content");
        var contentType = GetParameter<string>(parameters, "contentType", "text")!;

        var resolvedPhone = await ResolveRecipientAsync(recipient, provider, cancellationToken);
        if (resolvedPhone == null)
        {
            if (SelfAliases.Contains(recipient.Trim()))
            {
                return SkillResult.Error($"No '{provider}' identifier is configured for the owner. Open Settings -> Adresse Sekretariat and fill in the {provider} field.");
            }

            return SkillResult.Error($"No phone number found for '{recipient}'. The client must have a mobile or phone number stored in Klacks.");
        }

        var request = new SendMessageRequest(resolvedPhone.PhoneNumber, content, contentType);
        var result = await _messagingService.SendMessageAsync(provider, request, cancellationToken);

        if (!result.Success)
        {
            return SkillResult.Error($"Failed to send message via {provider}: {result.ErrorMessage}");
        }

        return SkillResult.SuccessResult(
            new
            {
                Provider = provider,
                Recipient = resolvedPhone.DisplayName,
                PhoneNumber = resolvedPhone.PhoneNumber,
                MessageId = result.ExternalMessageId,
                Status = "sent"
            },
            $"Message sent successfully via {provider} to {resolvedPhone.DisplayName} ({resolvedPhone.PhoneNumber}).");
    }

    private async Task<ResolvedPhone?> ResolveSelfAsync(string provider)
    {
        if (!ProviderToOwnerSettingKey.TryGetValue(provider, out var settingKey))
            return null;

        var ownerId = await _settingsReader.GetSettingAsync(settingKey);
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        var ownerName = await _settingsReader.GetSettingAsync("APP_ADDRESS_NAME");
        var displayName = string.IsNullOrWhiteSpace(ownerName) ? "Self" : ownerName!;
        return new ResolvedPhone(displayName, ownerId.Trim());
    }

    private async Task<ResolvedPhone?> ResolveRecipientAsync(string recipient, string provider, CancellationToken ct)
    {
        var trimmed = recipient.Trim();

        if (SelfAliases.Contains(trimmed))
        {
            return await ResolveSelfAsync(provider);
        }

        if (IsPhoneNumber(recipient))
        {
            return new ResolvedPhone(recipient, recipient);
        }

        var keywords = recipient.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keywords.Length == 0)
            return null;

        var whereClauses = keywords.Select((_, i) => $"(LOWER(COALESCE(c.\"FirstName\", '')) LIKE @p{i} OR LOWER(COALESCE(c.\"Name\", '')) LIKE @p{i} OR LOWER(COALESCE(c.\"Company\", '')) LIKE @p{i})");
        var whereClause = string.Join(" AND ", whereClauses);

        var sql = $"""
            SELECT c."Id", COALESCE(c."FirstName", '') AS "FirstName", COALESCE(c."Name", '') AS "Name",
                   cm."Value" AS "Phone", cm."Type" AS "CommType"
            FROM "Client" c
            INNER JOIN "Communication" cm ON cm."ClientId" = c."Id" AND cm."IsDeleted" = false
            WHERE c."IsDeleted" = false
              AND cm."Type" IN (1, 3, 0, 2)
              AND {whereClause}
            ORDER BY CASE cm."Type" WHEN 1 THEN 1 WHEN 3 THEN 2 WHEN 0 THEN 3 WHEN 2 THEN 4 END
            LIMIT 1
            """;

        var npgsqlParams = keywords.Select((k, i) => new Npgsql.NpgsqlParameter($"@p{i}", $"%{k}%")).ToArray();

        try
        {
            var rows = await _dbContext.Database
                .SqlQueryRaw<ClientPhoneRow>(sql, npgsqlParams)
                .ToListAsync(ct);

            var row = rows.FirstOrDefault();
            if (row == null)
                return null;

            var displayName = $"{row.FirstName} {row.Name}".Trim();
            return new ResolvedPhone(displayName, row.Phone);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPhoneNumber(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return false;
        if (trimmed.StartsWith('+')) return true;
        return trimmed.All(c => char.IsDigit(c) || c == '-' || c == ' ');
    }

    private record ResolvedPhone(string DisplayName, string PhoneNumber);
}

internal class ClientPhoneRow
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int CommType { get; set; }
}
