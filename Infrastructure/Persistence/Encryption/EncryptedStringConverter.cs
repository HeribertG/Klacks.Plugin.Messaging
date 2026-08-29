// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core value converter that encrypts string columns at rest via ASP.NET DataProtection.
/// Legacy plaintext values (without the "ENC:" prefix) are read back unchanged. Must not be used
/// on columns that are compared inside EF query predicates, because the encryption is
/// non-deterministic.
/// </summary>
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Encryption;

public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter()
        : base(
            v => MessagingFieldEncryption.Protect(v),
            v => MessagingFieldEncryption.Unprotect(v))
    {
    }
}
