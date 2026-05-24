using FitnessApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessApp.Infrastructure.Configurations;

public class UserTrainingBalanceConfiguration : IEntityTypeConfiguration<UserTrainingBalance>
{
    public void Configure(EntityTypeBuilder<UserTrainingBalance> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany(x => x.TrainingBalances)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.CreatedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.CreatedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.PurchaseType);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsExpired);
        builder.HasIndex(x => x.EndDate);
    }
}
