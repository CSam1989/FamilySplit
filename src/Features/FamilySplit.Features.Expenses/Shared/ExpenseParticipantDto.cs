namespace FamilySplit.Features.Expenses.Shared;

/// <summary>
/// A single participant's snapshotted weight and calculated share on an expense.
/// Shared by the List and GetDetail use cases.
/// </summary>
public record ExpenseParticipantDto(
    Guid Id,
    Guid FamilyMemberId,
    string DisplayName,
    Guid FamilyId,
    string FamilyName,
    decimal WeightSnapshot,
    decimal CalculatedAmount,
    bool IsExcluded);
