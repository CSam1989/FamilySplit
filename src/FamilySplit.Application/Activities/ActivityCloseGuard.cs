using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Activities;

/// <summary>Pure guards for the activity close operation.</summary>
public static class ActivityCloseGuard
{
    /// <summary>Returns true when the activity is in a state that can be closed.</summary>
    public static bool CanClose(ActivityStatus status) => status == ActivityStatus.Open;

    /// <summary>Returns true when the activity is a top-level activity (not a sub-activity).</summary>
    public static bool IsTopLevel(Guid? parentActivityId) => parentActivityId is null;
}
