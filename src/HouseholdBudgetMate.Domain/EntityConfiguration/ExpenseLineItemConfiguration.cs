using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class ExpenseLineItemConfiguration : IEntityTypeConfiguration<ExpenseLineItem>
{
    public void Configure(EntityTypeBuilder<ExpenseLineItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Amount)
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Expense)
            .WithMany(x => x.LineItems)
            .HasForeignKey(x => x.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag)
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

