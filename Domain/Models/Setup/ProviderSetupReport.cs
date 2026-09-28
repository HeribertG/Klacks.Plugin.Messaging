// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record ProviderSetupReport(string ProviderName, string ProviderType, bool IsEnabled, IReadOnlyList<SetupStep> Steps, SetupStep? NextStep);
