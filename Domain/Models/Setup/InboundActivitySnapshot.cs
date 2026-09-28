// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record InboundActivitySnapshot(DateTime? LastWebhookHitUtc, DateTime? LastSignatureRejectionUtc, IReadOnlyList<UnknownSenderSighting> UnknownSenders);
