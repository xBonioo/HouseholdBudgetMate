using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class LoanOperationAuditConfiguration : IEntityTypeConfiguration<LoanOperationAudit>
{
    public void Configure(EntityTypeBuilder<LoanOperationAudit> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.BudgetOwnerUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.OperationType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.ScheduleVersionBefore)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ScheduleVersionAfter)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.OperationPayloadJson)
            .IsRequired();

        builder.Property(x => x.RevertedByUserId)
            .HasMaxLength(128);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Loan)
            .WithMany()
            .HasForeignKey(x => x.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BudgetOwnerUser)
            .WithMany()
            .HasForeignKey(x => x.BudgetOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevertedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevertedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevertsOperation)
            .WithMany()
            .HasForeignKey(x => x.RevertsOperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RevertedByOperation)
            .WithMany()
            .HasForeignKey(x => x.RevertedByOperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.LoanId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.BudgetOwnerUserId);
        builder.HasIndex(x => x.OperationType);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.RevertsOperationId);
        builder.HasIndex(x => x.RevertedByOperationId);
    }
}
