// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The arguments of a send_broadcast call, read once for the skill and the host's preview. Parse throws ArgumentException for a
/// missing audience or an empty text. A broadcast is plain text: the content type is not a parameter.
/// </summary>
/// <param name="Audience">Group or client id numbers</param>
/// <param name="Content">The text, sent verbatim</param>
/// <param name="Provider">Provider name or type; null lets the planner pick the sole enabled provider</param>

namespace Klacks.Plugin.Messaging.Application.Models;

public sealed record SendBroadcastParameters(BroadcastAudienceParameters Audience, string Content, string? Provider)
{
    private const string ContentParameter = "content";
    private const string ProviderParameter = "provider";
    private const string ContentRequiredMessage = "Required parameter 'content' is missing";

    public static SendBroadcastParameters Parse(IReadOnlyDictionary<string, object> parameters)
    {
        var content = ReadText(parameters, ContentParameter);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(ContentRequiredMessage);
        }

        var provider = ReadText(parameters, ProviderParameter);
        return new SendBroadcastParameters(
            BroadcastAudienceParameters.Parse(parameters), content, string.IsNullOrWhiteSpace(provider) ? null : provider);
    }

    private static string? ReadText(IReadOnlyDictionary<string, object> parameters, string name) =>
        parameters.TryGetValue(name, out var value) && value is not null ? value as string ?? value.ToString() : null;
}
