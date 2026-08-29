// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Constants for at-rest encryption of messaging provider secrets.
/// </summary>
namespace Klacks.Plugin.Messaging.Application.Constants;

public static class MessagingEncryptionConstants
{
    /// <summary>
    /// Prefix marking an encrypted value. Matches the host's settings encryption convention so
    /// encrypted values are recognizable across the database and plaintext legacy values can be
    /// read unchanged.
    /// </summary>
    public const string EncryptedPrefix = "ENC:";

    /// <summary>
    /// DataProtection purpose string. Follows the host's "Klacks.[Area].Encryption" convention
    /// (settings use "Klacks.Settings.Encryption") while keeping an isolated key purpose for the
    /// messaging plugin.
    /// </summary>
    public const string DataProtectionPurpose = "Klacks.Messaging.Encryption";
}
