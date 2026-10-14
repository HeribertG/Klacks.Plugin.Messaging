// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The facts a send_message confirmation is bound to, in one place for the host's preview and the skill that sends: the provider,
/// the recipient address, the content type and a SHA-256 hash of the text (the text itself is never stored). The four items are
/// far below the host's storage cap, so the host always keeps them whole and the skill compares the items themselves: Matches is
/// true only for a binding of the message kind whose items equal the current ones, in whatever order the host stored them.
/// </summary>

using System.Security.Cryptography;
using System.Text;
using Klacks.Plugin.Contracts.Skills;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Domain.Models;

namespace Klacks.Plugin.Messaging.Application.Services;

public static class SendMessageBinding
{
    private const string ProviderPrefix = "provider:";
    private const string RecipientPrefix = "recipient:";
    private const string ContentTypePrefix = "content-type:";
    private const string ContentHashPrefix = "content-sha256:";

    public static IReadOnlyList<string> ItemsFor(MessageRecipientResolution resolution, string content, string contentType) =>
    [
        ProviderPrefix + resolution.ProviderName,
        RecipientPrefix + resolution.Identifier,
        ContentTypePrefix + contentType,
        ContentHashPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()
    ];

    public static bool Matches(PluginConfirmedBinding confirmed, IReadOnlyList<string> current) =>
        string.Equals(confirmed.Kind, MessagingConstants.MessageBindingKind, StringComparison.Ordinal)
        && confirmed.Items.Count > 0
        && confirmed.Items.OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(current.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal);
}
