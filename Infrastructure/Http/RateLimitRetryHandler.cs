// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Delegating handler that retries provider requests rejected with a throttling status.
/// Honours the Retry-After header when present and falls back to exponential backoff.
/// Only statuses that guarantee the request was not acted upon are retried (429, 503),
/// because outbound messages carry no idempotency key and a retry of an accepted request
/// would deliver the message twice.
/// </summary>
/// <param name="logger">Logger instance</param>
using System.Net;
using Klacks.Plugin.Messaging.Application.Constants;

namespace Klacks.Plugin.Messaging.Infrastructure.Http;

public class RateLimitRetryHandler : DelegatingHandler
{
    private readonly ILogger<RateLimitRetryHandler> _logger;

    public RateLimitRetryHandler(ILogger<RateLimitRetryHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var bufferedContent = await BufferContentAsync(request, cancellationToken);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        for (var attempt = 1; attempt <= MessagingRateLimitConstants.MaxRetryAttempts; attempt++)
        {
            if (!IsRetryable(response.StatusCode))
                return response;

            var delay = ResolveDelay(response, attempt);
            if (delay == null)
            {
                _logger.LogWarning(
                    "Provider request to {Uri} throttled with {StatusCode}, but the requested wait exceeds the allowed maximum. Giving up.",
                    request.RequestUri,
                    (int)response.StatusCode);
                return response;
            }

            _logger.LogWarning(
                "Provider request to {Uri} throttled with {StatusCode}. Retrying in {Delay}ms (attempt {Attempt}/{MaxAttempts}).",
                request.RequestUri,
                (int)response.StatusCode,
                delay.Value.TotalMilliseconds,
                attempt,
                MessagingRateLimitConstants.MaxRetryAttempts);

            response.Dispose();
            await Task.Delay(delay.Value, cancellationToken);

            var retryRequest = CloneRequest(request, bufferedContent);
            response = await base.SendAsync(retryRequest, cancellationToken);
        }

        if (IsRetryable(response.StatusCode))
        {
            _logger.LogError(
                "Provider request to {Uri} still throttled with {StatusCode} after {MaxAttempts} retries.",
                request.RequestUri,
                (int)response.StatusCode,
                MessagingRateLimitConstants.MaxRetryAttempts);
        }

        return response;
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
    }

    private static TimeSpan? ResolveDelay(HttpResponseMessage response, int attempt)
    {
        var requested = ReadRetryAfter(response) ?? ExponentialBackoff(attempt);

        if (requested > TimeSpan.FromMilliseconds(MessagingRateLimitConstants.MaxBackoffMilliseconds))
            return null;

        return requested < TimeSpan.Zero ? TimeSpan.Zero : requested;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
            return null;

        if (retryAfter.Delta.HasValue)
            return retryAfter.Delta.Value;

        if (retryAfter.Date.HasValue)
            return retryAfter.Date.Value - DateTimeOffset.UtcNow;

        return null;
    }

    private static TimeSpan ExponentialBackoff(int attempt)
    {
        var multiplier = 1 << (attempt - 1);
        return TimeSpan.FromMilliseconds(MessagingRateLimitConstants.BaseBackoffMilliseconds * multiplier);
    }

    private static async Task<byte[]?> BufferContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content == null)
            return null;

        return await request.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a fresh request for a retry. A HttpRequestMessage cannot be sent twice, so method,
    /// URI, headers and the buffered body are copied. The clone is deliberately not disposed:
    /// disposing it would dispose its content while the response returned to the caller may still
    /// be an unread stream, and a ByteArrayContent holds no unmanaged resource.
    /// </summary>
    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? bufferedContent)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        if (bufferedContent == null)
            return clone;

        clone.Content = new ByteArrayContent(bufferedContent);
        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
