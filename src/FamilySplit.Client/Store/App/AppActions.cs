namespace FamilySplit.Client.Store.App;

/// <summary>
/// Persists the context of the last activity the user added an expense to.
/// Used by the quick-add FAB to pre-select the most relevant activity.
/// </summary>
public record SetLastUsedActivityAction(
    Guid GroupId,
    Guid ActivityId,
    string ActivityName,
    string GroupName);
