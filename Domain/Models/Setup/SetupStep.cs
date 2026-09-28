// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

using Klacks.Plugin.Messaging.Domain.Enums;

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record SetupStep(string Code, SetupStepStatus Status, string? Detail = null, IReadOnlyDictionary<string, string>? Facts = null);
