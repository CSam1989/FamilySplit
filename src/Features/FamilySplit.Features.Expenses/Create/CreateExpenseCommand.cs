namespace FamilySplit.Features.Expenses.Create;

/// <summary>Request body for creating an expense on an activity.</summary>
public record CreateExpenseCommand(
    string Title,
    string? Description,
    decimal TotalAmount,
    string? Currency,
    DateOnly ExpenseDate,
    Guid? CategoryId);
