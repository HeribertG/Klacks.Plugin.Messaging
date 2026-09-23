// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Credential diagnosis for the Viber Bot/Channel API, kept as a partial class extension so the
/// main provider file stays focused on the messaging adapter contract.
/// </summary>
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class ViberMessagingProvider : ICredentialDiagnoser
{
    private const string PropWebhook = "webhook";

    public async Task<CredentialDiagnosis> DiagnoseCredentialsAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AuthToken))
            return new CredentialDiagnosis(false, CredentialReasonCodes.MissingToken);

        using var httpRequest = BuildRequest(GetAccountInfoUrl, EmptyJsonBody, config.AuthToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode && !VendorCredentialStatus.IsRejection(response.StatusCode))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(GetStringProperty(result, StatusMessageProperty)));

            if (!IsStatusOk(result))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(GetStringProperty(result, StatusMessageProperty)));

            var facts = new Dictionary<string, string>();
            var accountName = GetStringProperty(result, NameProperty);
            if (!string.IsNullOrWhiteSpace(accountName))
                facts[MessagingSetupConstants.FactAccountName] = accountName;

            var webhookUrl = GetStringProperty(result, PropWebhook);
            if (webhookUrl != null)
                facts[MessagingSetupConstants.FactWebhookUrl] = webhookUrl;

            return new CredentialDiagnosis(true, CredentialReasonCodes.Valid, null, facts.Count == 0 ? null : facts);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);
        }
        catch (JsonException)
        {
            return new CredentialDiagnosis(false, CredentialReasonCodes.UnexpectedResponse);
        }
    }
}
