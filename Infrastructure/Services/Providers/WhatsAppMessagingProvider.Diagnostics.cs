// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Credential diagnosis for the WhatsApp Cloud API (Meta Graph API), kept as a partial class
/// extension so the main provider file stays focused on the messaging adapter contract.
/// </summary>
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class WhatsAppMessagingProvider : ICredentialDiagnoser
{
    private const string DiagnosisFieldsQuery = "?fields=id,display_phone_number";
    private const string PropDisplayPhoneNumber = "display_phone_number";
    private const string PropErrorCode = "code";
    private const int GraphInvalidParameterCode = 100;
    private const int GraphPermissionDeniedCode = 10;
    private const int GraphAccessTokenInvalidCode = 190;
    private const int GraphPermissionRangeStart = 200;
    private const int GraphPermissionRangeEnd = 299;

    public async Task<CredentialDiagnosis> DiagnoseCredentialsAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.AccessToken) || string.IsNullOrWhiteSpace(config.PhoneNumberId))
            return new CredentialDiagnosis(false, CredentialReasonCodes.MissingToken);

        var url = $"{GraphApiBaseUrl}/{GraphApiVersion}/{config.PhoneNumberId}{DiagnosisFieldsQuery}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.AccessToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var isBadRequest = response.StatusCode == HttpStatusCode.BadRequest;
            if (!response.IsSuccessStatusCode && !isBadRequest && !VendorCredentialStatus.IsRejection(response.StatusCode))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);

            if (isBadRequest && !IsCredentialErrorCode(result))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);

            if (!response.IsSuccessStatusCode)
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(ExtractErrorMessage(responseBody)));

            var displayPhoneNumber = GetStringProperty(result, PropDisplayPhoneNumber);
            return new CredentialDiagnosis(true, CredentialReasonCodes.Valid, null,
                string.IsNullOrWhiteSpace(displayPhoneNumber)
                    ? null
                    : new Dictionary<string, string> { [MessagingSetupConstants.FactDisplayPhoneNumber] = displayPhoneNumber });
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

    private static bool IsCredentialErrorCode(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty(PropError, out var error)
            || error.ValueKind != JsonValueKind.Object
            || !error.TryGetProperty(PropErrorCode, out var codeElement)
            || !codeElement.TryGetInt32(out var code))
            return false;

        return code is GraphInvalidParameterCode or GraphPermissionDeniedCode or GraphAccessTokenInvalidCode
            || code is >= GraphPermissionRangeStart and <= GraphPermissionRangeEnd;
    }
}
