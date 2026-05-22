using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> b)
    {
        b.ToTable("family_members");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.FamilyId).HasColumnName("family_id").IsRequired();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.IsAdmin).HasColumnName("is_admin").HasDefaultValue(false).IsRequired();
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        b.Property(x => x.WeightOverride).HasColumnName("weight_override").HasColumnType("decimal(4,2)");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        // Each FamilyMember belongs to exactly one Family.
        b.HasOne(x => x.Family)
            .WithMany(f => f.Members)
            .HasForeignKey(x => x.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        // A FamilyMember may optionally be linked to a User (null = child / passive member).
        b.HasOne(x => x.User)
            .WithOne(u => u.FamilyMember)
            .HasForeignKey<FamilyMember>(x => x.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Email is unique when present (nulls excluded from uniqueness).
        b.HasIndex(x => x.Email).IsUnique().HasFilter("email IS NOT NULL");
    }
}
