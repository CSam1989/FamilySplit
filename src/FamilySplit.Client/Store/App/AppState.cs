using Fluxor;

namespace FamilySplit.Client.Store.App;

/// <summary>
/// Top-level app state placeholder so Fluxor has something to scan in Phase 1.
/// Domain-specific features (Groups, Activities, Expenses, ...) land in their own slices.
/// </summary>
[FeatureState]
public record AppState(bool Initialized)
{
    public AppState() : this(Initialized: false) { }
}
