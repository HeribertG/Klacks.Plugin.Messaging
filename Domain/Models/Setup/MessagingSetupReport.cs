// Copyright (c) Heribert Gasparoli Private. All rights reserved.

namespace Klacks.Plugin.Messaging.Domain.Models.Setup;

public sealed record MessagingSetupReport(IReadOnlyList<SetupStep> PluginSteps, IReadOnlyList<ProviderSetupReport> Providers);
