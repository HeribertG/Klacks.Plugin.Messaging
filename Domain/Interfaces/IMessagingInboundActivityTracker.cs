// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Process-local record of inbound activity per provider (webhook hits, signature rejections, discarded
/// unknown senders) so the setup diagnosis can tell "never arrived" from "arrived from an unknown sender".
/// </summary>
/// <param name="providerId">The provider the activity belongs to</param>
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface IMessagingInboundActivityTracker
{
    void RecordWebhookHit(Guid providerId);

    void RecordSignatureRejection(Guid providerId);

    void RecordUnknownSender(Guid providerId, string senderId, string? displayName);

    InboundActivitySnapshot GetSnapshot(Guid providerId);
}
