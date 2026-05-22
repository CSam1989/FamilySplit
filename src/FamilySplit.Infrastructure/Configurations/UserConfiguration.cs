using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(255).IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
        b.Property(x => x.IsGlobalAdmin).HasColumnName("is_global_admin").HasDefaultValue(false).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();
    }
}
