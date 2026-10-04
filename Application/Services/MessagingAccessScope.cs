// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Applies the host's group visibility to messages and broadcast audiences for non-admin callers. The message
/// list reads raw pages restricted to client messages and keeps only those of visible clients, up to
/// MaxVisibleMessagePageScans pages, so a supervisor still gets full pages while hidden messages are interleaved.
/// Each client is checked once per call.
/// </summary>
/// <param name="messagingService">Source of the raw message pages</param>
/// <param name="clientVisibility">The host's group visibility for one client</param>
/// <param name="clientGroupReader">Members of a group hierarchy, the unrestricted broadcast audience</param>
/// <param name="clientIdNumberReader">Clients behind id numbers, the unrestricted id-number audience</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public class MessagingAccessScope : IMessagingAccessScope
{
    private readonly IMessagingService _messagingService;
    private readonly IClientVisibilityReader _clientVisibility;
    private readonly IClientGroupReader _clientGroupReader;
    private readonly IClientIdNumberReader _clientIdNumberReader;

    public MessagingAccessScope(
        IMessagingService messagingService,
        IClientVisibilityReader clientVisibility,
        IClientGroupReader clientGroupReader,
        IClientIdNumberReader clientIdNumberReader)
    {
        _messagingService = messagingService;
        _clientVisibility = clientVisibility;
        _clientGroupReader = clientGroupReader;
        _clientIdNumberReader = clientIdNumberReader;
    }

    public async Task<IReadOnlyList<Message>> GetVisibleMessagesAsync(
        Guid? providerId, MessageDirection? direction, string? sender, MessageScope? scope, int count, int offset, CancellationToken ct = default)
    {
        if (scope == MessageScope.Internal)
        {
            return Array.Empty<Message>();
        }

        count = Math.Clamp(count, MessagingConstants.MinMessageQueryCount, MessagingConstants.MaxMessageQueryCount);
        offset = Math.Max(offset, 0);
        var needed = offset + count;
        const int rawPageSize = MessagingConstants.MaxMessageQueryCount;

        var visibility = new Dictionary<Guid, bool>();
        var visibleNewestFirst = new List<Message>();

        for (var scan = 0; scan < MessagingConstants.MaxVisibleMessagePageScans && visibleNewestFirst.Count < needed; scan++)
        {
            var page = await _messagingService.GetMessagesAsync(
                providerId, direction, sender, MessageScope.Client, rawPageSize, scan * rawPageSize, ct);

            for (var i = page.Count - 1; i >= 0; i--)
            {
                if (await IsVisibleCachedAsync(page[i].ClientId, visibility, ct))
                {
                    visibleNewestFirst.Add(page[i]);
                }
            }

            if (page.Count < rawPageSize)
            {
                break;
            }
        }

        var result = visibleNewestFirst.Skip(offset).Take(count).ToList();
        result.Reverse();
        return result;
    }

    public Task<bool> IsMessageVisibleAsync(Message message, CancellationToken ct = default)
    {
        return IsClientVisibleAsync(message.ClientId, ct);
    }

    public async Task<bool> IsClientVisibleAsync(Guid? clientId, CancellationToken ct = default)
    {
        return clientId.HasValue && await _clientVisibility.IsClientVisibleAsync(clientId.Value, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetVisibleGroupClientIdsAsync(Guid groupId, CancellationToken ct = default)
    {
        var clientIds = await _clientGroupReader.GetClientIdsInGroupAsync(groupId, ct);
        return await FilterVisibleAsync(clientIds, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetVisibleIdNumberClientIdsAsync(IReadOnlyCollection<int> idNumbers, CancellationToken ct = default)
    {
        var clientIds = await _clientIdNumberReader.GetClientIdsByIdNumbersAsync(idNumbers, ct);
        return await FilterVisibleAsync(clientIds, ct);
    }

    private async Task<IReadOnlyList<Guid>> FilterVisibleAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken ct)
    {
        var visible = new List<Guid>();
        foreach (var clientId in clientIds.Distinct())
        {
            if (await _clientVisibility.IsClientVisibleAsync(clientId, ct))
            {
                visible.Add(clientId);
            }
        }

        return visible;
    }

    private async Task<bool> IsVisibleCachedAsync(Guid? clientId, Dictionary<Guid, bool> visibility, CancellationToken ct)
    {
        if (!clientId.HasValue)
        {
            return false;
        }

        if (!visibility.TryGetValue(clientId.Value, out var isVisible))
        {
            isVisible = await _clientVisibility.IsClientVisibleAsync(clientId.Value, ct);
            visibility[clientId.Value] = isVisible;
        }

        return isVisible;
    }
}
