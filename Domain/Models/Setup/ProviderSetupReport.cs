// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record ProviderSetupReport(string ProviderName, string ProviderType, bool IsEnabled, IReadOnlyList<SetupStep> Steps, SetupStep? NextStep);
