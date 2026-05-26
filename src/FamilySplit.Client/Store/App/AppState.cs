using Fluxor;

namespace FamilySplit.Client.Store.App;

[FeatureState]
public record AppState
{
    public bool Initialized { get; init; }

    // ── Last-used activity (drives the quick-add FAB) ─────────────────────────
    /// <summary>The group the user most recently added an expense to.</summary>
    public Guid? LastUsedGroupId { get; init; }
    /// <summary>The activity the user most recently added an expense to.</summary>
    public Guid? LastUsedActivityId { get; init; }
    public string? LastUsedActivityName { get; init; }
    public string? LastUsedGroupName { get; init; }
}
