using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Activities.Shared;

/// <summary>
/// Lightweight summary shown in the activity list for a group and as the
/// sub-activity entries on an activity's detail. Shared by the List and
/// GetDetail queries (read-side wire-format lock).
/// </summary>
public record ActivitySummaryDto(
    Guid Id,
    Guid GroupId,
    string Name,
    string? Description,
    ActivityStatus Status,
    Guid? ParentActivityId,
    int ParticipantCount,
    int SubActivityCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    int ExpenseCount = 0,
    decimal TotalExpenseAmount = 0m,
    string ExpenseCurrency = "EUR");
