// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Optional adapter capability: checks the provider credentials against the vendor API and returns
/// a reason instead of a bare boolean. Must never post a message and never return secrets.
/// </summary>
/// <param name="configJson">Provider-specific configuration JSON</param>
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Domain.Interfaces;

public interface ICredentialDiagnoser
{
    Task<CredentialDiagnosis> DiagnoseCredentialsAsync(string configJson, CancellationToken ct = default);
}
