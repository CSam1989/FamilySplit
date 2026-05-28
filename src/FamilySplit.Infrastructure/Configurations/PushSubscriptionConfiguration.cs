using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.ToTable("push_subscriptions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        b.Property(x => x.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(2048)
            .IsRequired();

        b.Property(x => x.P256dh)
            .HasColumnName("p256dh")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Auth)
            .HasColumnName("auth")
            .HasMaxLength(128)
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Endpoint is the natural key — one subscription row per browser/device.
        b.HasIndex(x => x.Endpoint).IsUnique();

        // Fan-out lookup: "give me all subscriptions for user X"
        b.HasIndex(x => x.UserId);
    }
}
