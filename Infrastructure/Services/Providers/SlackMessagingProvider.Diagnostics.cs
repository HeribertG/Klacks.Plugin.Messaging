// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Credential diagnosis for the Slack Web API, kept as a partial class extension so the main
/// provider file stays focused on the messaging adapter contract.
/// </summary>
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using Klacks.Plugin.Messaging.Application.Constants;
using Klacks.Plugin.Messaging.Application.Services.Setup;
using Klacks.Plugin.Messaging.Domain.Interfaces;
using Klacks.Plugin.Messaging.Domain.Models.Setup;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public partial class SlackMessagingProvider : ICredentialDiagnoser
{
    private const string TeamProperty = "team";

    public async Task<CredentialDiagnosis> DiagnoseCredentialsAsync(string configJson, CancellationToken ct = default)
    {
        var config = DeserializeConfig(configJson);
        if (config == null || string.IsNullOrWhiteSpace(config.BotToken))
            return new CredentialDiagnosis(false, CredentialReasonCodes.MissingToken);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AuthTestUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, config.BotToken);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode && !VendorCredentialStatus.IsRejection(response.StatusCode))
                return new CredentialDiagnosis(false, CredentialReasonCodes.Unreachable);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(GetStringProperty(result, ErrorProperty)));

            if (!result.TryGetProperty(OkProperty, out var ok) || ok.ValueKind != JsonValueKind.True)
                return new CredentialDiagnosis(false, CredentialReasonCodes.Rejected, VendorText.Truncate(GetStringProperty(result, ErrorProperty)));

            var facts = BuildFacts(result);

            if (response.Headers.TryGetValues(MessagingSetupConstants.SlackScopesHeader, out var scopeValues)
                && !HasRequiredScope(scopeValues))
            {
                var missingScopeFacts = new Dictionary<string, string>(facts)
                {
                    [MessagingSetupConstants.FactMissingScope] = MessagingSetupConstants.SlackRequiredScopeChatWrite
                };
                return new CredentialDiagnosis(false, CredentialReasonCodes.MissingScope, null, missingScopeFacts);
            }

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

    private static bool HasRequiredScope(IEnumerable<string> scopeHeaderValues)
    {
        return scopeHeaderValues
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Contains(MessagingSetupConstants.SlackRequiredScopeChatWrite, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> BuildFacts(JsonElement result)
    {
        var facts = new Dictionary<string, string>();
        var team = GetStringProperty(result, TeamProperty);
        var botUser = GetStringProperty(result, UserProperty);

        if (!string.IsNullOrWhiteSpace(team))
            facts[MessagingSetupConstants.FactTeam] = team;

        if (!string.IsNullOrWhiteSpace(botUser))
            facts[MessagingSetupConstants.FactBotUser] = botUser;

        return facts;
    }
}
