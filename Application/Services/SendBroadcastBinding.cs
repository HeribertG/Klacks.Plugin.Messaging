// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The facts a send_broadcast confirmation is bound to, for the host's preview and the skill that sends. They are a fixed handful
/// of digests, never one item per recipient: the host keeps only a small number of items and hands a plugin nothing else, so a
/// list per person would arrive empty for any larger group. The recipient digest covers client, address and how the address was
/// found; the text is hashed and never stored. A changed member, a changed address or a changed text all fail Matches.
/// </summary>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public static class SendBroadcastBinding
{
    private const string ProviderPrefix = "provider:";
    private const string RecipientCountPrefix = "recipient-count:";
    private const string RecipientsHashPrefix = "recipients-sha256:";
    private const string ContentTypePrefix = "content-type:";
    private const string ContentHashPrefix = "content-sha256:";
    private const string CompactGuidFormat = "N";
    private const char FieldSeparator = '|';
    private const char LineSeparator = '\n';

    public static IReadOnlyList<string> ItemsFor(BroadcastRecipientSet recipients, string content) =>
    [
        ProviderPrefix + recipients.ProviderName,
        RecipientCountPrefix + recipients.Recipients.Count.ToString(CultureInfo.InvariantCulture),
        RecipientsHashPrefix + Sha256(string.Join(
            LineSeparator,
            recipients.Recipients
                .Select(recipient => string.Join(FieldSeparator, recipient.ClientId.ToString(CompactGuidFormat), recipient.Recipient, recipient.FromPhoneFallback))
                .OrderBy(line => line, StringComparer.Ordinal))),
        ContentTypePrefix + MessagingConstants.DefaultContentType,
        ContentHashPrefix + Sha256(content)
    ];

    public static bool Matches(PluginConfirmedBinding confirmed, IReadOnlyList<string> current) =>
        string.Equals(confirmed.Kind, MessagingConstants.BroadcastBindingKind, StringComparison.Ordinal)
        && ConfirmedBindingItems.AreEqual(confirmed.Items, current);

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
