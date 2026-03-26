// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core configuration for the MessagingProvider entity with unique name index and property constraints.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;

public class MessagingProviderConfiguration : IEntityTypeConfiguration<MessagingProvider>
{
    public void Configure(EntityTypeBuilder<MessagingProvider> builder)
    {
        builder.ToTable("messaging_providers");
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ProviderType).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ConfigJson).IsRequired();
        builder.Property(p => p.WebhookSecret).HasMaxLength(256);
    }
}
