// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Sanitizes vendor-supplied error text before it is stored in a credential diagnosis result:
/// collapses line breaks and cuts it to the shared maximum length.
/// </summary>
using System.Text.RegularExpressions;
using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Application.Services.Setup;

public static class VendorText
{
    private static readonly Regex LineBreakPattern = new(@"[\r\n]+", RegexOptions.Compiled);

    public static string? Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var collapsed = LineBreakPattern.Replace(text, " ").Trim();
        return collapsed.Length <= MessagingSetupConstants.MaxVendorMessageLength
            ? collapsed
            : collapsed[..MessagingSetupConstants.MaxVendorMessageLength];
    }
}
