// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core configuration for the Message entity with foreign key, property constraints and indexes.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasOne(m => m.Provider)
            .WithMany()
            .HasForeignKey(m => m.ProviderId)
            .IsRequired();

        builder.Property(m => m.ExternalMessageId).HasMaxLength(256);
        builder.Property(m => m.Sender).HasMaxLength(100).IsRequired();
        builder.Property(m => m.SenderDisplayName).HasMaxLength(200);
        builder.Property(m => m.Recipient).HasMaxLength(100).IsRequired();
        builder.Property(m => m.RecipientDisplayName).HasMaxLength(200);
        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(50);

        builder.HasIndex(m => new { m.ProviderId, m.Timestamp }).IsDescending(false, true);
        builder.HasIndex(m => m.Direction);
        builder.HasIndex(m => m.ClientId);
        builder.HasIndex(m => m.BroadcastId);
    }
}
