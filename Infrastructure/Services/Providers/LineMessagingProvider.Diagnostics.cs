// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Credential diagnosis for the LINE Messaging API, kept as a partial class extension so the main
/// provider file stays focused on the messaging adapter contract.
/// </summary>
using System.Net.Http.Headers;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class LineMessagingProvider : ICredentialDiagnoser
{
    private const string PropBasicId = "basicId";

    public async Task<CredentialDiagnosis> DiagnoseCredentialsAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.ChannelAccessToken))
            return new CredentialDiagnosis(false, CredentialReasonCodes.MissingToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BotInfoUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.ChannelAccessToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode && !VendorCredentialStatus.IsRejection(response.StatusCode))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(GetStringProperty(result, PropMessage)));

            var basicId = GetStringProperty(result, PropBasicId);
            return new CredentialDiagnosis(true, CredentialReasonCodes.Valid, null,
                string.IsNullOrWhiteSpace(basicId) ? null : new Dictionary<string, string> { [MessagingSetupConstants.FactBasicId] = basicId });
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
