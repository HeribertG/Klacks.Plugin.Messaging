// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Fixed English detail texts the messenger setup diagnosis attaches to a step. They are the only
/// explanation source the assistant may use, so they state facts and never guess a cause.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class SetupStepDetails
{
    public const string NoProviderConfigured = "No messaging provider is configured";
    public const string NoProviderEnabled = "Messaging providers exist but none is enabled";
    public const string TeamsNotTested = "Test would post a message into the channel";
    public const string SkippedUntilRequiredFields = "Skipped until the required fields are filled in";
    public const string SkippedUntilCredentialsValid = "Skipped until the credentials are valid";
    public const string SkippedUntilWebhookUrlUsable = "Skipped until the webhook URL is usable";
    public const string CredentialsRejected = "The vendor rejected the configured credentials";
    public const string CredentialErrorFormat = "{0}: {1}";
    public const string WebhookUrlMismatch = "The vendor has a different webhook URL registered than the one configured";
    public const string WebhookInfoUnavailable = "The vendor did not return webhook information";
    public const string SignatureRejected = "Webhook calls fail signature or verify-token validation; the endpoint is public, so a stray or foreign caller can cause this too - if the vendor console shows successful deliveries, the provider secret or verify token does not match";
    public const string RecentUnknownSenders = "Messages arrived from recent unknown sender(s) and were discarded; only senders linked to an employee or to the owner are kept";
    public const string NoInboundYet = "No inbound message observed yet; send a test message to the bot";
    public const string NoOwnerIdentity = "No owner messenger identity of this type is configured";
    public const string NoOwnerIdentityWithUnknownSenders = "No owner messenger identity of this type is configured; the most recent unknown sender is listed as a possible candidate";
    public const string SendFailedWithoutMessage = "Sending failed without a vendor error message";
    public const string EmployeeCountUnavailable = "The number of employees could not be determined";
    public const string RedactedPlaceholder = "[redacted]";
    public const string ListSeparator = ",";
}
