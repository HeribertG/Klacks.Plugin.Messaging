// Copyright (c) Heribert Gasparoli Private. All rights reserved.

/// <summary>
/// EF Core configuration for TelegramOnboardingToken with unique token index and composite lookup index.
/// </summary>
using Klacks.Plugin.Messaging.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klacks.Plugin.Messaging.Infrastructure.Persistence.Configurations;

public class TelegramOnboardingTokenConfiguration : IEntityTypeConfiguration<TelegramOnboardingToken>
{
    public void Configure(EntityTypeBuilder<TelegramOnboardingToken> builder)
    {
        builder.ToTable("telegram_onboarding_token");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ClientId).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.RedeemedChatId).HasMaxLength(64);
        builder.Property(t => t.IsDeleted).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => new { t.ClientId, t.UsedAt, t.IsDeleted });
    }
}
