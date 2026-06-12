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
// Strict CQRS: the create command returns only the new id; the effect re-queries
// the list rather than patching state from a returned DTO.
public record CreateExpenseAction(Guid GroupId, Guid ActivityId, CreateExpenseRequest Request);
public record CreateExpenseSuccessAction;
public record CreateExpenseFailureAction(string ErrorMessage);

// ── Update ────────────────────────────────────────────────────────────────────
// Strict CQRS: the update command returns 204; the effect re-queries the list.
public record UpdateExpenseAction(Guid GroupId, Guid ActivityId, Guid ExpenseId, UpdateExpenseRequest Request);
public record UpdateExpenseSuccessAction;
public record UpdateExpenseFailureAction(string ErrorMessage);

// ── Delete ────────────────────────────────────────────────────────────────────
public record DeleteExpenseAction(Guid GroupId, Guid ActivityId, Guid ExpenseId);
public record DeleteExpenseSuccessAction(Guid ExpenseId);
public record DeleteExpenseFailureAction(string ErrorMessage);

// ── Clear ─────────────────────────────────────────────────────────────────────
public record ClearExpensesAction;
public record ClearExpenseErrorAction;
