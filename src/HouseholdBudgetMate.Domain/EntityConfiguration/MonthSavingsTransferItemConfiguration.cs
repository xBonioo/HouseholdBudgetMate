using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class MonthSavingsTransferItemConfiguration : IEntityTypeConfiguration<MonthSavingsTransferItem>
{
    public void Configure(EntityTypeBuilder<MonthSavingsTransferItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Amount)
            .IsRequired();

        builder.HasIndex(x => x.TransferDate);
        builder.Property(x => x.TransferDate)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.MonthPlanId);
        builder.HasOne(x => x.MonthPlan)
            .WithMany(x => x.SavingsTransfers)
            .HasForeignKey(x => x.MonthPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}