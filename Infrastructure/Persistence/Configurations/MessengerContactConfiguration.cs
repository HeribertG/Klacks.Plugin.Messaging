// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core configuration for the MessengerContact entity with index on client_id and constraints.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;

public class MessengerContactConfiguration : IEntityTypeConfiguration<MessengerContact>
{
    public void Configure(EntityTypeBuilder<MessengerContact> builder)
    {
        builder.ToTable("messenger_contact");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ClientId).IsRequired();
        builder.Property(c => c.Type).IsRequired();
        builder.Property(c => c.Value).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(200);
        builder.Property(c => c.IsDeleted).IsRequired();
        builder.Property(c => c.CreateTime).IsRequired();
        builder.HasIndex(c => c.ClientId);
        builder.HasIndex(c => new { c.ClientId, c.Type });
    }
}
