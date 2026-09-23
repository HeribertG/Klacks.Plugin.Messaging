// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Removes the provider's credential values (the per-type secret keys from ProviderSecretConfigKeys, under
/// any casing and including duplicates) and the server-generated webhook secret from vendor-supplied text
/// before it enters the setup report, then truncates it. Identifiers and Klacks' own webhook URL stay
/// readable. Defense in depth: stored send errors and vendor messages are untrusted text and the report
/// is forwarded to an external LLM.
/// </summary>
/// <param name="providerType">Provider type that decides which configuration keys are credentials</param>
/// <param name="configJson">Provider configuration holding the credential values</param>
/// <param name="webhookSecret">Server-generated webhook secret of the provider</param>
using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public sealed class SetupSecretRedactor
{
    private readonly IReadOnlyList<string> _secrets;

    public SetupSecretRedactor(string providerType, string? configJson, string? webhookSecret)
    {
        _secrets = SetupConfigReader.GetAllValuesOf(configJson, ProviderSecretConfigKeys.For(providerType))
            .Append(webhookSecret ?? string.Empty)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToList();
    }

    public string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = _secrets.Aggregate(text, (current, secret) =>
            current.Replace(secret, SetupStepDetails.RedactedPlaceholder, StringComparison.Ordinal));

        return VendorText.Truncate(cleaned);
    }
}
