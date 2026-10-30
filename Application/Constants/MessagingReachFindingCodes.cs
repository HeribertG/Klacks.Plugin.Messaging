// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Codes of the findings of evaluate_messaging_reach. NoVisibleClients covers an empty, an unknown and a hidden audience alike.
/// </summary>

namespace Klacks.Plugin.Messaging.Application.Constants;

public static class MessagingReachFindingCodes
{
    public const string ReachableViaChannel = "ReachableViaChannel";

    public const string SendOnlyNoReply = "SendOnlyNoReply";

    public const string ProviderDisabled = "ProviderDisabled";

    public const string NoMessenger = "NoMessenger";

    public const string NoEnabledChannel = "NoEnabledChannel";

    public const string NoVisibleClients = "NoVisibleClients";
}
