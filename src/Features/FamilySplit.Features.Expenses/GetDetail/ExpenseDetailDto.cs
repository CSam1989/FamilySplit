using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Shared;

namespace FamilySplit.Features.Expenses.GetDetail;

public record ExpenseDetailDto(
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
    List<ExpenseParticipantDto> Participants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
