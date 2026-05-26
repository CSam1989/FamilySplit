namespace FamilySplit.Client.Services;

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
