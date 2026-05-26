namespace FamilySplit.Application.Dashboard.Dtos;

/// <summary>
/// Per-group statistics shown on the dashboard home page.
/// </summary>
/// <param name="GroupId">The group's unique identifier.</param>
/// <param name="GroupName">Display name of the group.</param>
/// <param name="TotalActivities">Count of top-level activities (all statuses).</param>
/// <param name="OpenActivities">Count of activities currently open.</param>
/// <param name="ClosedActivities">Count of closed (but not yet settled) activities.</param>
/// <param name="SettledActivities">Count of fully settled activities.</param>
/// <param name="TotalGroupSpend">Sum of all expense amounts across all activities in this group (all families combined, full history).</param>
/// <param name="MyFamilyShare">Sum of the caller's family's calculated share across all expenses in this group (full history).</param>
/// <param name="ActiveGroupSpend">Sum of expenses in Open + Closed activities only (excludes settled).</param>
/// <param name="ActiveFamilyShare">Caller's family's calculated share in Open + Closed activities only.</param>
/// <param name="Currency">Most common currency used in the group's expenses (default EUR).</param>
/// <param name="NetBalance">
///     Caller's family net balance across Open + Closed activities only.
///     Positive  = the family is owed money (creditor).
///     Negative  = the family owes money (debtor).
///     Zero      = balanced.
/// </param>
/// <param name="PendingSettlements">
///     Number of settlements requiring the caller's family to act:
///     either confirm-sent (Proposed, caller is payer) or confirm-received (PayerSent, caller is receiver).
/// </param>
/// <param name="LatestActivityName">Name of the most recently created top-level activity, or null if none.</param>
/// <param name="LatestActivityStatus">Status string of the most recent activity, or null if none.</param>
public record DashboardGroupStatDto(
    Guid GroupId,
    string GroupName,
    int TotalActivities,
    int OpenActivities,
    int ClosedActivities,
    int SettledActivities,
    decimal TotalGroupSpend,
    decimal MyFamilyShare,
    decimal ActiveGroupSpend,
    decimal ActiveFamilyShare,
    string Currency,
    decimal NetBalance,
    int PendingSettlements,
    string? LatestActivityName,
    string? LatestActivityStatus
);
