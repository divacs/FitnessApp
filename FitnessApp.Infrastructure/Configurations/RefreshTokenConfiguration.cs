using FitnessApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessApp.Infrastructure.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ReplacedByToken)
            .HasMaxLength(256);

        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsRevoked);
        builder.Ignore(x => x.IsActive);

        builder.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Token)
            .IsUnique();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
