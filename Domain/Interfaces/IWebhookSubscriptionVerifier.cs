// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Optional adapter capability for verifying webhook subscription handshakes (e.g. Meta hub.verify_token).
/// </summary>
/// <param name="configJson">Provider-specific configuration JSON</param>
/// <param name="verifyToken">The verify token sent by the external platform</param>
namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IWebhookSubscriptionVerifier
{
    bool VerifySubscription(string configJson, string verifyToken);
}
