using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Expenses;

// ── Load List ─────────────────────────────────────────────────────────────────
public record LoadExpensesAction(Guid GroupId, Guid ActivityId);
public record LoadExpensesSuccessAction(List<ExpenseSummaryDto> Expenses);
public record LoadExpensesFailureAction(string ErrorMessage);

// ── Load Detail ───────────────────────────────────────────────────────────────
public record LoadExpenseDetailAction(Guid GroupId, Guid ActivityId, Guid ExpenseId);
public record LoadExpenseDetailSuccessAction(ExpenseDetailDto Expense);
public record LoadExpenseDetailFailureAction(string ErrorMessage);

// ── Create ────────────────────────────────────────────────────────────────────
public record CreateExpenseAction(Guid GroupId, Guid ActivityId, CreateExpenseRequest Request);
public record CreateExpenseSuccessAction(ExpenseDetailDto Expense);
public record CreateExpenseFailureAction(string ErrorMessage);

// ── Update ────────────────────────────────────────────────────────────────────
public record UpdateExpenseAction(Guid GroupId, Guid ActivityId, Guid ExpenseId, UpdateExpenseRequest Request);
public record UpdateExpenseSuccessAction(ExpenseDetailDto Expense);
public record UpdateExpenseFailureAction(string ErrorMessage);

// ── Delete ────────────────────────────────────────────────────────────────────
public record DeleteExpenseAction(Guid GroupId, Guid ActivityId, Guid ExpenseId);
public record DeleteExpenseSuccessAction(Guid ExpenseId);
public record DeleteExpenseFailureAction(string ErrorMessage);

// ── Clear ─────────────────────────────────────────────────────────────────────
public record ClearExpensesAction;
public record ClearExpenseErrorAction;
