// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The arguments of a send_message call, read once for everything that must agree on them: the skill that sends, the recipient
/// resolver and the host's preview of the call. Parse accepts the string, JSON or other values a model call carries and throws
/// ArgumentException for a missing recipient or content.
/// </summary>
/// <param name="RecipientType">'client' (default) or 'user', lower-cased</param>
/// <param name="Recipient">Owner alias, client name, address, or application user name</param>
/// <param name="Content">The message text</param>
/// <param name="ContentType">text, image or document (default text)</param>
/// <param name="Provider">Provider name or type; null lets the resolver pick the sole enabled provider (or the user's preferred channel)</param>

namespace Klacks.Plugin.Messaging.Application.Models;

public sealed record SendMessageParameters(
    string RecipientType,
    string Recipient,
    string Content,
    string ContentType,
    string? Provider)
{
    public const string RecipientTypeClient = "client";
    public const string RecipientTypeUser = "user";

    private const string RecipientTypeParameter = "recipientType";
    private const string RecipientParameter = "recipient";
    private const string ContentParameter = "content";
    private const string ContentTypeParameter = "contentType";
    private const string ProviderParameter = "provider";
    private const string DefaultContentType = "text";
    private const string MissingParameterMessage = "Required parameter '{0}' is missing";

    public static SendMessageParameters Parse(IReadOnlyDictionary<string, object> parameters)
    {
        var recipientType = ReadText(parameters, RecipientTypeParameter) ?? RecipientTypeClient;
        var recipient = ReadText(parameters, RecipientParameter)
            ?? throw new ArgumentException(string.Format(MissingParameterMessage, RecipientParameter));
        var content = ReadText(parameters, ContentParameter)
            ?? throw new ArgumentException(string.Format(MissingParameterMessage, ContentParameter));
        var contentType = ReadText(parameters, ContentTypeParameter) ?? DefaultContentType;

        return new SendMessageParameters(
            recipientType.Trim().ToLowerInvariant(), recipient, content, contentType, ReadText(parameters, ProviderParameter));
    }

    private static string? ReadText(IReadOnlyDictionary<string, object> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value as string ?? value.ToString();
    }
}
