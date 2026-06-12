using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Expenses.List;

public record ExpenseSummaryDto(
    Guid Id,
    Guid ActivityId,
    string Title,
    string? Description,
    decimal TotalAmount,
    string Currency,
    DateOnly ExpenseDate,
    string PaidByName,
    Guid PaidByFamilyId,
    string PaidByFamilyName,
    ExpenseStatus Status,
    int ParticipantCount,
    DateTimeOffset CreatedAt);
