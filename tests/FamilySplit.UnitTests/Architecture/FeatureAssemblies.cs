using System.Reflection;

namespace FamilySplit.UnitTests.Architecture;

/// <summary>
/// Registry of feature-slice assemblies. Each slice phase appends its assembly
/// here so the architecture rules are automatically applied to the new slice.
/// Empty until Phase 3 — all rules are vacuously green in this state.
/// </summary>
internal static class FeatureAssemblies
{
    internal static readonly Assembly[] All = [];
}
