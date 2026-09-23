// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Implements IPersonalRecipientClassifier for Slack: a recipient is personal when it is a Slack user
/// id, which starts with 'U' (regular user) or 'W' (enterprise-grid user) and contains only letters and
/// digits. Channel ids ('C'), private-group/MPIM ids ('G') and DM-channel ids ('D') are not personal.
/// </summary>

using System.Linq;
using Klacks.Plugin.Messaging.Domain.Interfaces;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class SlackMessagingProvider : IPersonalRecipientClassifier
{
    private static readonly char[] PersonalUserIdPrefixes = ['U', 'W'];

    public bool IsPersonalRecipient(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            return false;

        var value = recipient.Trim();
        return value.Length > 1
            && PersonalUserIdPrefixes.Contains(value[0])
            && value.All(char.IsLetterOrDigit);
    }
}
