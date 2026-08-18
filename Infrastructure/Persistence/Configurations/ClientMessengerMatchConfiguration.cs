// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core configuration for ClientMessengerMatch as a keyless view entity, used only as the
/// projection target of MessengerContactRepository.SearchByClientNameAsync's raw SQL.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;

public class ClientMessengerMatchConfiguration : IEntityTypeConfiguration<ClientMessengerMatch>
{
    public void Configure(EntityTypeBuilder<ClientMessengerMatch> builder)
    {
        builder.HasNoKey().ToView(null);
    }
}
