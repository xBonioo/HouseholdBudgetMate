using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(128)
            .ValueGeneratedNever();

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.HouseholdMode)
            .IsRequired();

        builder.Property(x => x.IsAdmin)
            .IsRequired();

        builder.Property(x => x.BudgetOwnerUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Username)
            .IsUnique();

        builder.HasOne(x => x.BudgetOwnerUser)
            .WithMany(x => x.SharedBudgetUsers)
            .HasForeignKey(x => x.BudgetOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
