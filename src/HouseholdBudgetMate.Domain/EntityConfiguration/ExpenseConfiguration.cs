using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PlannedAmount)
            .IsRequired();

        builder.Property(x => x.ActualAmount)
            .IsRequired();

        builder.Property(x => x.Order)
            .IsRequired();

        builder.Property(x => x.ShowRemainingInUI)
            .IsRequired();
        
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.MonthPlan)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.MonthPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tag)
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RegularExpenseDefinition)
            .WithMany()
            .HasForeignKey(x => x.RegularExpenseDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.LoanInstallment)
            .WithOne(x => x.Expense)
            .HasForeignKey<Expense>(x => x.LoanInstallmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.MonthPlanId, x.Order });
        builder.HasIndex(x => new { x.MonthPlanId, x.RegularExpenseDefinitionId })
            .IsUnique();
        builder.HasIndex(x => x.LoanInstallmentId)
            .IsUnique();
    }
}

