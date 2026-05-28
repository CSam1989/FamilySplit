using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Expenses;

[FeatureState]
public record ExpenseState
{
    public IReadOnlyList<ExpenseSummaryDto> Expenses { get; init; } = [];
    public ExpenseDetailDto? SelectedExpense { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
