// Copyright (c) Heribert Gasparoli. SPDX-License-Identifier: AGPL-3.0-only

/// <summary>
/// Fixed step codes for the messenger setup diagnosis; MessagingSetupDiagnosticsService emits them in commissioning order.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class SetupStepCodes
{
    public const string ProviderPresent = "ProviderPresent";
    public const string ProviderEnabled = "ProviderEnabled";
    public const string RequiredFields = "RequiredFields";
    public const string Credentials = "Credentials";
    public const string WebhookUrl = "WebhookUrl";
    public const string WebhookRegistered = "WebhookRegistered";
    public const string InboundObserved = "InboundObserved";
    public const string OwnerIdentity = "OwnerIdentity";
    public const string LastSendFailure = "LastSendFailure";
    public const string EmployeesReachable = "EmployeesReachable";
    public const string KnownLimitation = "KnownLimitation";
}
