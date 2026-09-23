// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Limits and fact/limitation keys shared by the messenger setup diagnosis.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class MessagingSetupConstants
{
    public const int MaxVendorMessageLength = 200;
    public const int MaxUnknownSendersPerProvider = 5;
    public const int UnknownSenderTtlHours = 24;
    public const int SendFailureLookbackDays = 7;
    public const int SendFailureScanCount = 20;
    public const string SlackRequiredScopeChatWrite = "chat:write";
    public const string SlackScopesHeader = "x-oauth-scopes";
    public const string FactUnknownSenderId = "unknownSenderId";
    public const string FactUnknownSenderName = "unknownSenderName";
    public const string FactWebhookUrl = "webhookUrl";
    public const string FactMissingFields = "missingFields";
    public const string FactBotUsername = "botUsername";
    public const string FactTeam = "team";
    public const string FactBotUser = "botUser";
    public const string FactDisplayPhoneNumber = "displayPhoneNumber";
    public const string FactAccountName = "accountName";
    public const string FactBasicId = "basicId";
    public const string FactMissingScope = "missingScope";
    public const string FactPendingUpdates = "pendingUpdates";
    public const string FactLinkedClients = "linkedClients";
    public const string FactTotalEmployees = "totalEmployees";
    public const string FactRegisteredWebhookUrl = "registeredWebhookUrl";
    public const int MaxUnknownSenderNameLength = 64;
    public const int ProviderVendorTimeoutSeconds = 15;
    public const string FactLimitation = "limitation";
    public const string LimitationSlackPairing = "SlackPairingNotRedeemable";
    public const string LimitationMultipleProvidersPairing = "PairingNeedsSingleEnabledProvider";
    public const string LimitationTelegramNeedsWebhook = "TelegramOnboardingNeedsWebhook";
    public const string ManualConsoleRegistration = "RegisterInVendorConsole";
}
