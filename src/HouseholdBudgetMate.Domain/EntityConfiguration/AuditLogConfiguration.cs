using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EntityType)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.BudgetOwnerUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Operation)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.OldValuesJson)
            .IsRequired();

        builder.Property(x => x.NewValuesJson)
            .IsRequired();

        builder.Property(x => x.ChangedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BudgetOwnerUser)
            .WithMany()
            .HasForeignKey(x => x.BudgetOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BudgetOwnerUserId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.EntityType);
        builder.HasIndex(x => x.Operation);
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}
