using System.Reflection;

namespace FamilySplit.UnitTests.Architecture;

/// <summary>
/// Registry of feature-slice assemblies. Each slice phase appends its assembly
/// here so the architecture rules are automatically applied to the new slice.
/// </summary>
internal static class FeatureAssemblies
{
    internal static readonly Assembly[] All =
    [
        typeof(FamilySplit.Features.Expenses.ExpensesModule).Assembly,
        typeof(FamilySplit.Features.Dashboard.DashboardModule).Assembly,
        typeof(FamilySplit.Features.Users.UsersModule).Assembly,
    ];
}
