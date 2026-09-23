// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Provider configuration keys the messenger setup diagnosis reads from ConfigJson.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class SetupConfigKeys
{
    public const string WebhookUrl = "WebhookUrl";
    public const string ChannelId = "ChannelId";
    public const string DefaultChannel = "DefaultChannel";
    public const string SigningSecret = "SigningSecret";
}
