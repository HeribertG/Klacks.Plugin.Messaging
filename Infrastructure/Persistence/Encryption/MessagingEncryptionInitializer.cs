// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// Hosted service that initializes the static messaging field encryption bridge at application
/// start. Registered as the first messaging plugin service so it starts before any hosted service
/// that could materialize or save messaging provider entities.
/// </summary>
/// <param name="dataProtectionProvider">Host DataProtection provider supplying the key ring</param>
/// <param name="logger">Logger handed to the static encryption bridge for decrypt warnings</param>
using Microsoft.AspNetCore.DataProtection;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Encryption;

public class MessagingEncryptionInitializer : IHostedService
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<MessagingEncryptionInitializer> _logger;

    public MessagingEncryptionInitializer(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MessagingEncryptionInitializer> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        MessagingFieldEncryption.Initialize(_dataProtectionProvider, _logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
