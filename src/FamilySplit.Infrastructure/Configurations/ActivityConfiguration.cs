using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> b)
    {
        b.ToTable("activities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.GroupId).HasColumnName("group_id").IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.ParentActivityId).HasColumnName("parent_activity_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(25).IsRequired();
        b.Property(x => x.ClosedAt).HasColumnName("closed_at");
        b.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        b.HasOne(x => x.Group)
            .WithMany(g => g.Activities)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ParentActivity)
            .WithMany(p => p.SubActivities)
            .HasForeignKey(x => x.ParentActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.GroupId);
        b.HasIndex(x => x.ParentActivityId);
    }
}
