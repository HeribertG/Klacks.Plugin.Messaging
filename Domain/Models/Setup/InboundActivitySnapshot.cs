// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record InboundActivitySnapshot(DateTime? LastWebhookHitUtc, DateTime? LastSignatureRejectionUtc, IReadOnlyList<UnknownSenderSighting> UnknownSenders);
