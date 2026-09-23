// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Classifies a failed vendor HTTP status for the credential diagnosis: only 401 and 403 mean the vendor
/// rejected the credentials; every other failure (429, 5xx, ...) says nothing about them and is reported
/// as Unreachable. Telegram additionally answers 404 for a malformed bot token.
/// </summary>
/// <param name="statusCode">HTTP status code of a non-successful vendor response</param>
using System.Net;

namespace Klacks.Plugin.Messaging.Infrastructure.Services.Providers;

public static class VendorCredentialStatus
{
    public static bool IsRejection(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    public static bool IsTelegramRejection(HttpStatusCode statusCode) =>
        IsRejection(statusCode) || statusCode == HttpStatusCode.NotFound;
}
