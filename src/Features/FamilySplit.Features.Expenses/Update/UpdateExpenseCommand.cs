namespace FamilySplit.Features.Expenses.Update;

/// <summary>Request body for updating an expense.</summary>
public record UpdateExpenseCommand(
    string Title,
    string? Description,
    decimal TotalAmount,
    string? Currency,
    DateOnly ExpenseDate,
    Guid? CategoryId);
