using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid PaidByUserId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateOnly ExpenseDate { get; set; }
    public Guid? CategoryId { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Activity Activity { get; set; } = default!;
    public User PaidBy { get; set; } = default!;
    public Category? Category { get; set; }
    public ICollection<ExpenseParticipant> Participants { get; set; } = new List<ExpenseParticipant>();
}
