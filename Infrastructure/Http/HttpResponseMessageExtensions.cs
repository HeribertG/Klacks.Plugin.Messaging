// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Extensions classifying provider HTTP responses for the messaging adapters.
/// </summary>
/// <param name="response">The provider response to classify</param>
using System.Net;

namespace Klacks.Plugin.Messaging.Infrastructure.Http;

public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// True when the provider rejected the request because of rate limiting. Deliberately narrower
    /// than the set of statuses the retry handler resends on: a 503 means the provider is down,
    /// which is worth retrying but must not be reported as throttling, or an outage would be
    /// misattributed to the send pace.
    /// </summary>
    public static bool IsThrottled(this HttpResponseMessage response)
    {
        return response.StatusCode == HttpStatusCode.TooManyRequests;
    }
}
