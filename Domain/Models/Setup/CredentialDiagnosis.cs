// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record CredentialDiagnosis(bool IsValid, string ReasonCode, string? VendorMessage = null, IReadOnlyDictionary<string, string>? Facts = null);
