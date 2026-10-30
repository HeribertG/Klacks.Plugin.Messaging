// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The broadcast address rule: per client the messenger contact of the provider's type, otherwise the mobile number where the
/// provider accepts one, otherwise the client is skipped. Used by the send path, its preview and the reach advice, so the three
/// cannot count differently. Contacts and phone numbers are read in one query each.
/// </summary>
/// <param name="messengerContactRepository">Messenger contacts of the audience</param>
/// <param name="clientPhoneReader">Mobile numbers of the audience</param>

using Klacks.Plugin.Contracts;
using Klacks.Plugin.Messaging.Application.Interfaces;
using Klacks.Plugin.Messaging.Domain.Enums;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public class BroadcastRecipientResolver : IBroadcastRecipientResolver
{
    private readonly IMessengerContactRepository _messengerContactRepository;
    private readonly IClientPhoneReader _clientPhoneReader;

    public BroadcastRecipientResolver(IMessengerContactRepository messengerContactRepository, IClientPhoneReader clientPhoneReader)
    {
        _messengerContactRepository = messengerContactRepository;
        _clientPhoneReader = clientPhoneReader;
    }

    public async Task<(IReadOnlyList<BroadcastRecipient> Recipients, int Skipped)> ResolveAsync(
        string providerType, bool supportsPhoneAsRecipient, IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MessengerType>(providerType, ignoreCase: true, out var messengerType))
            throw new InvalidOperationException($"Cannot map provider type '{providerType}' to MessengerType");

        var contacts = await _messengerContactRepository.GetFirstContactsAsync(clientIds, messengerType, ct);
        IReadOnlyDictionary<Guid, string?> phoneFallbacks = supportsPhoneAsRecipient
            ? await _clientPhoneReader.GetMobilePhonesAsync(clientIds, ct)
            : new Dictionary<Guid, string?>();

        var recipients = new List<BroadcastRecipient>();
        var skipped = 0;

        foreach (var clientId in clientIds)
        {
            if (contacts.TryGetValue(clientId, out var contact) && !string.IsNullOrWhiteSpace(contact.Value))
            {
                recipients.Add(new BroadcastRecipient(clientId, contact.Value, contact.Description, FromPhoneFallback: false));
                continue;
            }

            if (phoneFallbacks.TryGetValue(clientId, out var phone) && !string.IsNullOrWhiteSpace(phone))
            {
                recipients.Add(new BroadcastRecipient(clientId, phone!, null, FromPhoneFallback: true));
                continue;
            }

            skipped++;
        }

        return (recipients, skipped);
    }
}
