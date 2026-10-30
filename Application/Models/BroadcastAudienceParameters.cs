// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// The audience of a broadcast skill call: exactly one group or a list of client id numbers. Parse accepts the string, number,
/// array and JSON values a model call or a stored confirmation carries and throws ArgumentException for anything else. An empty
/// group id is refused like in MessagingController, because the UI uses it as the "all groups" marker.
/// </summary>
/// <param name="GroupId">Root group of the audience; its subgroups belong to it</param>
/// <param name="IdNumbers">Client id numbers of the audience</param>

using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace Klacks.Plugin.Messaging.Application.Models;

public sealed record BroadcastAudienceParameters(Guid? GroupId, IReadOnlyList<int> IdNumbers)
{
    private const string GroupIdParameter = "groupId";
    private const string IdNumbersParameter = "idNumbers";
    private const string AudienceRequiredMessage = "Name exactly one audience: either 'groupId' or 'idNumbers'.";
    private const string InvalidGroupIdMessage = "Parameter 'groupId' must be the id of a group.";
    private const string InvalidIdNumbersMessage = "Parameter 'idNumbers' must be a list of whole numbers.";

    private static readonly char[] ListSeparators = [',', ';', ' '];

    public static BroadcastAudienceParameters Parse(IReadOnlyDictionary<string, object> parameters)
    {
        var groupText = ReadText(parameters, GroupIdParameter);
        var idNumbers = ReadIdNumbers(parameters);

        if ((groupText == null) == (idNumbers.Count == 0))
        {
            throw new ArgumentException(AudienceRequiredMessage);
        }

        if (groupText == null)
        {
            return new BroadcastAudienceParameters(null, idNumbers);
        }

        if (!Guid.TryParse(groupText, out var groupId) || groupId == Guid.Empty)
        {
            throw new ArgumentException(InvalidGroupIdMessage);
        }

        return new BroadcastAudienceParameters(groupId, []);
    }

    private static string? ReadText(IReadOnlyDictionary<string, object> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value as string ?? value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static List<int> ReadIdNumbers(IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue(IdNumbersParameter, out var value) || value is null)
        {
            return [];
        }

        return Tokens(value).Select(ParseNumber).Distinct().ToList();
    }

    private static IEnumerable<string> Tokens(object value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(item => item.ToString()),
        JsonElement element => SplitList(element.ToString()),
        string text => SplitList(text),
        IEnumerable items => items.Cast<object?>().Select(item => item?.ToString() ?? string.Empty),
        _ => SplitList(value.ToString() ?? string.Empty)
    };

    private static string[] SplitList(string text) =>
        text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int ParseNumber(string token) =>
        int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ArgumentException(InvalidIdNumbersMessage);
}
