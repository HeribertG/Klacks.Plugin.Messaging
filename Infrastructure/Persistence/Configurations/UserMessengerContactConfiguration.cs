// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core configuration for the UserMessengerContact entity with lookup indexes and a partial
/// unique index that allows at most one preferred channel per user. No foreign key to the Identity
/// table on purpose: the plugin owns this table and must stay removable without leaving constraints
/// on the core schema, exactly like MessengerContact does for Client.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;

public class UserMessengerContactConfiguration : IEntityTypeConfiguration<UserMessengerContact>
{
    private const string PreferredPerUserIndexName = "ix_user_messenger_contact_user_preferred";
    private const string PreferredPerUserFilter = "\"is_preferred\" = true AND \"is_deleted\" = false";

    public void Configure(EntityTypeBuilder<UserMessengerContact> builder)
    {
        builder.ToTable("user_messenger_contact");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UserId).HasMaxLength(450).IsRequired();
        builder.Property(c => c.Type).IsRequired();
        builder.Property(c => c.Value).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(200);
        builder.Property(c => c.IsPreferred).IsRequired();
        builder.Property(c => c.IsDeleted).IsRequired();
        builder.Property(c => c.CreateTime).IsRequired();

        // The composite index also serves the plain lookup by user, so no separate single-column
        // index is declared - a second one on UserId would collide with the partial unique index.
        builder.HasIndex(c => new { c.UserId, c.Type });

        builder.HasIndex(c => c.UserId, PreferredPerUserIndexName)
            .HasDatabaseName(PreferredPerUserIndexName)
            .IsUnique()
            .HasFilter(PreferredPerUserFilter);
    }
}
