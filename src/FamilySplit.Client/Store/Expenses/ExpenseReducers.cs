using Fluxor;

namespace FamilySplit.Client.Store.Expenses;

public static class ExpenseReducers
{
    // ── Load List ─────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadExpensesAction))]
    public static ExpenseState OnLoad(ExpenseState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ExpenseState OnLoadSuccess(ExpenseState state, LoadExpensesSuccessAction action) =>
        state with { IsLoading = false, Expenses = action.Expenses };

    [ReducerMethod]
    public static ExpenseState OnLoadFailure(ExpenseState state, LoadExpensesFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Load Detail ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadExpenseDetailAction))]
    public static ExpenseState OnLoadDetail(ExpenseState state) =>
        state with { IsLoading = true, ErrorMessage = null, SelectedExpense = null };

    [ReducerMethod]
    public static ExpenseState OnLoadDetailSuccess(ExpenseState state, LoadExpenseDetailSuccessAction action) =>
        state with { IsLoading = false, SelectedExpense = action.Expense };

    [ReducerMethod]
    public static ExpenseState OnLoadDetailFailure(ExpenseState state, LoadExpenseDetailFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Create ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(CreateExpenseAction))]
    public static ExpenseState OnCreate(ExpenseState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ExpenseState OnCreateSuccess(ExpenseState state, CreateExpenseSuccessAction action) =>
        state with { IsLoading = false, SelectedExpense = action.Expense };

    [ReducerMethod]
    public static ExpenseState OnCreateFailure(ExpenseState state, CreateExpenseFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Update ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateExpenseAction))]
    public static ExpenseState OnUpdate(ExpenseState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ExpenseState OnUpdateSuccess(ExpenseState state, UpdateExpenseSuccessAction action) =>
        state with { IsLoading = false, SelectedExpense = action.Expense };

    [ReducerMethod]
    public static ExpenseState OnUpdateFailure(ExpenseState state, UpdateExpenseFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Delete ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(DeleteExpenseAction))]
    public static ExpenseState OnDelete(ExpenseState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ExpenseState OnDeleteSuccess(ExpenseState state, DeleteExpenseSuccessAction action) =>
        state with
        {
            IsLoading = false,
            Expenses = state.Expenses.Where(e => e.Id != action.ExpenseId).ToList(),
            SelectedExpense = state.SelectedExpense?.Id == action.ExpenseId ? null : state.SelectedExpense,
        };

    [ReducerMethod]
    public static ExpenseState OnDeleteFailure(ExpenseState state, DeleteExpenseFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Clear ─────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ClearExpensesAction))]
    public static ExpenseState OnClear(ExpenseState state) =>
        state with { Expenses = [], SelectedExpense = null, ErrorMessage = null };

    [ReducerMethod(typeof(ClearExpenseErrorAction))]
    public static ExpenseState OnClearError(ExpenseState state) =>
        state with { ErrorMessage = null };
}
