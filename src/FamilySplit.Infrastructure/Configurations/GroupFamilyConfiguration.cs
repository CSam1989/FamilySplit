using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class GroupFamilyConfiguration : IEntityTypeConfiguration<GroupFamily>
{
    public void Configure(EntityTypeBuilder<GroupFamily> b)
    {
        b.ToTable("group_families");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.GroupId).HasColumnName("group_id").IsRequired();
        b.Property(x => x.FamilyId).HasColumnName("family_id").IsRequired();
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()").IsRequired();

        b.HasOne(x => x.Group)
            .WithMany(g => g.GroupFamilies)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Family)
            .WithMany(f => f.GroupFamilies)
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        // A family can only be in a group once.
        b.HasIndex(x => new { x.GroupId, x.FamilyId }).IsUnique();
    }
}
