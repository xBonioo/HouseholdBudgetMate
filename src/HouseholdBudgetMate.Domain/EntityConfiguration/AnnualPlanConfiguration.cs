using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class AnnualPlanConfiguration : IEntityTypeConfiguration<AnnualPlan>
{
    public void Configure(EntityTypeBuilder<AnnualPlan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Year)
            .IsRequired();

        builder.Property(x => x.ExpectedIncomeAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ExpectedSavingsAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Year })
            .IsUnique();
    }
}
