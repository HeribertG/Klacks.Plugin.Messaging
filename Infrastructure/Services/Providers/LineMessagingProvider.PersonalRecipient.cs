// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Implements IPersonalRecipientClassifier for LINE: a recipient is personal when it is a user id,
/// which starts with 'U'. Group ids ('C') and room ids ('R') are not personal.
/// </summary>

using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class LineMessagingProvider : IPersonalRecipientClassifier
{
    private const char UserIdPrefix = 'U';

    public bool IsPersonalRecipient(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            return false;

        var value = recipient.Trim();
        return value.Length > 1 && value[0] == UserIdPrefix;
    }
}
