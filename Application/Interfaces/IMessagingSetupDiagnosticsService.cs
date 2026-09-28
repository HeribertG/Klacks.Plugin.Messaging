// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Deterministic, read-only diagnosis of the messenger commissioning state: plugin-level steps plus one
/// report per provider with its steps in commissioning order and the next open step. Never contains secrets.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Application.Interfaces;

public interface IMessagingSetupDiagnosticsService
{
    Task<MessagingSetupReport> DiagnoseAsync(CancellationToken ct = default);
}
