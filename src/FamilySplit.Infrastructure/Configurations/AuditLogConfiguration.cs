using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        b.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        b.Property(x => x.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("now()").IsRequired();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.Timestamp);
    }
}
