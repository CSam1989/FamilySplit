using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> b)
    {
        b.ToTable("settlements");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ActivityId).HasColumnName("activity_id").IsRequired();
        b.Property(x => x.PayerFamilyId).HasColumnName("payer_family_id").IsRequired();
        b.Property(x => x.ReceiverFamilyId).HasColumnName("receiver_family_id").IsRequired();
        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(12,2)").IsRequired();
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        b.Property(x => x.ProposedAt).HasColumnName("proposed_at").HasDefaultValueSql("now()").IsRequired();
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");

        b.ToTable(t => t.HasCheckConstraint("ck_settlements_amount_positive", "amount > 0"));

        b.HasOne(x => x.Activity)
            .WithMany(a => a.Settlements)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PayerFamily)
            .WithMany()
            .HasForeignKey(x => x.PayerFamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ReceiverFamily)
            .WithMany()
            .HasForeignKey(x => x.ReceiverFamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ActivityId);
    }
}
