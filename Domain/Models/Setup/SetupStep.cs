// Copyright (c) Heribert Gasparoli Private. All rights reserved.

using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record SetupStep(string Code, SetupStepStatus Status, string? Detail = null, IReadOnlyDictionary<string, string>? Facts = null);
