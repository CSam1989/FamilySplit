using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> b)
    {
        b.ToTable("approval_steps");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.SettlementId).HasColumnName("settlement_id").IsRequired();
        b.Property(x => x.ApproverId).HasColumnName("approver_id").IsRequired();
        b.Property(x => x.StepType).HasColumnName("step_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ActionedAt).HasColumnName("actioned_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        b.HasOne(x => x.Settlement)
            .WithMany(s => s.ApprovalSteps)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
