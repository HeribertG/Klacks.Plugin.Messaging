// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Pre-flight summary for a broadcast operation. Returned by the preview endpoint so the
/// caller can show the user how many clients will be reached before the actual send.
/// </summary>
/// <param name="Total">Total number of clients in the target group</param>
/// <param name="WithMessengerContact">Recipients reachable via an explicit MessengerContact for the provider</param>
/// <param name="WithPhoneFallback">Recipients reachable via mobile phone fallback (only when the provider supports phone numbers)</param>
/// <param name="Skipped">Recipients without any usable identifier</param>
/// <param name="ProviderSupportsPhoneFallback">Whether the chosen provider can use mobile phone numbers as a fallback identifier</param>

namespace Klacks.Plugin.Messaging.Domain.Models;

public record BroadcastPreview(
    int Total,
    int WithMessengerContact,
    int WithPhoneFallback,
    int Skipped,
    bool ProviderSupportsPhoneFallback);
