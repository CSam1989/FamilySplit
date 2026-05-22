using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class ExpenseParticipantConfiguration : IEntityTypeConfiguration<ExpenseParticipant>
{
    public void Configure(EntityTypeBuilder<ExpenseParticipant> b)
    {
        b.ToTable("expense_participants");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ExpenseId).HasColumnName("expense_id").IsRequired();
        b.Property(x => x.FamilyMemberId).HasColumnName("family_member_id").IsRequired();
        b.Property(x => x.WeightSnapshot).HasColumnName("weight_snapshot").HasColumnType("decimal(4,2)").IsRequired();
        b.Property(x => x.CalculatedAmount).HasColumnName("calculated_amount").HasColumnType("decimal(12,2)").IsRequired();
        b.Property(x => x.IsExcluded).HasColumnName("is_excluded").HasDefaultValue(false);

        b.HasOne(x => x.Expense)
            .WithMany(e => e.Participants)
            .HasForeignKey(x => x.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.FamilyMember)
            .WithMany()
            .HasForeignKey(x => x.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ExpenseId, x.FamilyMemberId }).IsUnique();
    }
}
