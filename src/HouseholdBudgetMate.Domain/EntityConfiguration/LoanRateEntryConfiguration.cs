using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class LoanRateEntryConfiguration : IEntityTypeConfiguration<LoanRateEntry>
{
    public void Configure(EntityTypeBuilder<LoanRateEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EffectiveFrom)
            .IsRequired();

        builder.Property(x => x.ReferenceRate)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.LoanId, x.EffectiveFrom })
            .IsUnique();
    }
}