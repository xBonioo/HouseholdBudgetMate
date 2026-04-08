using HouseholdBudgetMate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdBudgetMate.Domain.EntityConfiguration;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SupportsLineItemsOverride);

        builder.HasOne(x => x.ParentTag)
            .WithMany(x => x.ChildTags)
            .HasForeignKey(x => x.ParentTagId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.CategoryId, x.ParentTagId });
    }
}