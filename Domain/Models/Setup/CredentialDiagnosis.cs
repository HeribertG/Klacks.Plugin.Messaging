// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record CredentialDiagnosis(bool IsValid, string ReasonCode, string? VendorMessage = null, IReadOnlyDictionary<string, string>? Facts = null);
