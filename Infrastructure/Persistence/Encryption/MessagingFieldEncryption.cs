// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Static encryption bridge used by the EF Core value converter for messaging provider secrets.
/// A static holder is required because plugin model configuration (IPluginRegistrar.ConfigureDbModel)
/// runs without access to the host's service provider; MessagingEncryptionInitializer fills it at
/// application start before any messaging entity is materialized or saved. Values without the
/// "ENC:" prefix are passed through unchanged so pre-existing plaintext rows keep loading.
/// </summary>
using Klacks.Plugin.Messaging.Application.Constants;
using Microsoft.AspNetCore.DataProtection;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Encryption;

public static class MessagingFieldEncryption
{
    private static IDataProtector? _protector;
    private static ILogger? _logger;

    public static void Initialize(IDataProtectionProvider dataProtectionProvider, ILogger logger)
    {
        _protector = dataProtectionProvider.CreateProtector(MessagingEncryptionConstants.DataProtectionPurpose);
        _logger = logger;
    }

    public static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith(MessagingEncryptionConstants.EncryptedPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        var protector = _protector
            ?? throw new InvalidOperationException(
                "Messaging field encryption is not initialized; refusing to store a provider secret in plaintext.");

        return MessagingEncryptionConstants.EncryptedPrefix + protector.Protect(value);
    }

    public static string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(MessagingEncryptionConstants.EncryptedPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        var protector = _protector
            ?? throw new InvalidOperationException(
                "Messaging field encryption is not initialized; cannot decrypt a stored provider secret.");

        try
        {
            return protector.Unprotect(value[MessagingEncryptionConstants.EncryptedPrefix.Length..]);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to decrypt an ENC:-prefixed messaging provider value. The DataProtection key used to encrypt it is no longer in the key ring. Re-save the provider to re-encrypt it with the current key. Treating the value as not configured.");
            return string.Empty;
        }
    }
}
