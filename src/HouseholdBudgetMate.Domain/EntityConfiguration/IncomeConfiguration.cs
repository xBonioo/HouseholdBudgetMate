using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Year)
            .IsRequired();

        builder.Property(x => x.Month)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Amount)
            .IsRequired();

        builder.Property(x => x.ExpectedDayOfMonth)
            .IsRequired();

        builder.Property(x => x.IsRegular)
            .IsRequired();

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RegularIncomeDefinition)
            .WithMany()
            .HasForeignKey(x => x.RegularIncomeDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Year, x.Month });
        builder.HasIndex(x => new { x.Year, x.Month, x.RegularIncomeDefinitionId })
            .IsUnique();
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.RegularIncomeDefinitionId);
    }
}
