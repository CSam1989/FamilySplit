using FamilySplit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilySplit.Infrastructure.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("expenses");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ActivityId).HasColumnName("activity_id").IsRequired();
        b.Property(x => x.PaidByUserId).HasColumnName("paid_by_user_id").IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        b.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(12,2)").IsRequired();
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.ExpenseDate).HasColumnName("expense_date").IsRequired();
        b.Property(x => x.CategoryId).HasColumnName("category_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        b.ToTable(t => t.HasCheckConstraint("ck_expenses_total_amount_positive", "total_amount > 0"));

        b.HasOne(x => x.Activity)
            .WithMany(a => a.Expenses)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PaidBy)
            .WithMany()
            .HasForeignKey(x => x.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ActivityId);
    }
}
