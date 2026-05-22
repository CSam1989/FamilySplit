using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class ActivityParticipantConfiguration : IEntityTypeConfiguration<ActivityParticipant>
{
    public void Configure(EntityTypeBuilder<ActivityParticipant> b)
    {
        b.ToTable("activity_participants");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ActivityId).HasColumnName("activity_id").IsRequired();
        b.Property(x => x.FamilyMemberId).HasColumnName("family_member_id").IsRequired();

        b.HasOne(x => x.Activity)
            .WithMany(a => a.Participants)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.FamilyMember)
            .WithMany()
            .HasForeignKey(x => x.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.ActivityId, x.FamilyMemberId }).IsUnique();
    }
}
