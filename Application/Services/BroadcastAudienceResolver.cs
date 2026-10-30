// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Resolves a broadcast audience with the branches of MessagingController: an administrator reads the group or the id numbers
/// directly, every other caller goes through the group visibility, and a hidden client counts as absent. The empty-audience
/// refusal carries the controller's texts, so a hidden group cannot be told apart from an empty one.
/// </summary>
/// <param name="clientGroupReader">Members of a group hierarchy</param>
/// <param name="clientIdNumberReader">Clients behind id numbers</param>
/// <param name="accessScope">Group visibility of the calling user</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Application.Models;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public class BroadcastAudienceResolver : IBroadcastAudienceResolver
{
    private readonly IClientGroupReader _clientGroupReader;
    private readonly IClientIdNumberReader _clientIdNumberReader;
    private readonly IMessagingAccessScope _accessScope;

    public BroadcastAudienceResolver(
        IClientGroupReader clientGroupReader, IClientIdNumberReader clientIdNumberReader, IMessagingAccessScope accessScope)
    {
        _clientGroupReader = clientGroupReader;
        _clientIdNumberReader = clientIdNumberReader;
        _accessScope = accessScope;
    }

    public async Task<BroadcastAudience> ResolveAsync(BroadcastAudienceParameters parameters, bool isAdmin, CancellationToken ct = default)
    {
        if (parameters.GroupId is { } groupId)
        {
            var groupClients = isAdmin
                ? await _clientGroupReader.GetClientIdsInGroupAsync(groupId, ct)
                : await _accessScope.GetVisibleGroupClientIdsAsync(groupId, ct);
            return new BroadcastAudience(groupClients.Distinct().ToList(), MessagingConstants.BroadcastGroupEmptyError);
        }

        var clients = isAdmin
            ? await _clientIdNumberReader.GetClientIdsByIdNumbersAsync(parameters.IdNumbers, ct)
            : await _accessScope.GetVisibleIdNumberClientIdsAsync(parameters.IdNumbers, ct);
        return new BroadcastAudience(clients.Distinct().ToList(), MessagingConstants.BroadcastNoClientsForIdNumbersError);
    }
}
