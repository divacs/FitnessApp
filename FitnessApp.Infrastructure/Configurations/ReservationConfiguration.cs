using FitnessApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessApp.Infrastructure.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.TrainingSession)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.TrainingSessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TrainingSessionId);
        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => new { x.UserId, x.TrainingSessionId, x.Status });

    }
}
