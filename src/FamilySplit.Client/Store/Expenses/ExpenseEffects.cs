using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Settlements;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Expenses;

public class ExpenseEffects
{
    private readonly IExpenseClient _client;
    private readonly ILogger<ExpenseEffects> _logger;

    public ExpenseEffects(IExpenseClient client, ILogger<ExpenseEffects> logger)
    {
        _client = client;
        _logger = logger;
    }

    [EffectMethod]
    public async Task HandleLoad(LoadExpensesAction action, IDispatcher dispatcher)
    {
        try
        {
            var expenses = await _client.ListAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new LoadExpensesSuccessAction(expenses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expenses for activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new LoadExpensesFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoadDetail(LoadExpenseDetailAction action, IDispatcher dispatcher)
    {
        try
        {
            var expense = await _client.GetAsync(action.GroupId, action.ActivityId, action.ExpenseId);
            dispatcher.Dispatch(new LoadExpenseDetailSuccessAction(expense));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expense {ExpenseId}", action.ExpenseId);
            dispatcher.Dispatch(new LoadExpenseDetailFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleCreate(CreateExpenseAction action, IDispatcher dispatcher)
    {
        try
        {
            var expense = await _client.CreateAsync(action.GroupId, action.ActivityId, action.Request);
            dispatcher.Dispatch(new CreateExpenseSuccessAction(expense));
            // Refresh list and re-compute live balance.
            dispatcher.Dispatch(new LoadExpensesAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadBalancesAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create expense for activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new CreateExpenseFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleUpdate(UpdateExpenseAction action, IDispatcher dispatcher)
    {
        try
        {
            var expense = await _client.UpdateAsync(action.GroupId, action.ActivityId, action.ExpenseId, action.Request);
            dispatcher.Dispatch(new UpdateExpenseSuccessAction(expense));
            // Refresh list and re-compute live balance.
            dispatcher.Dispatch(new LoadExpensesAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadBalancesAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update expense {ExpenseId}", action.ExpenseId);
            dispatcher.Dispatch(new UpdateExpenseFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleDelete(DeleteExpenseAction action, IDispatcher dispatcher)
    {
        try
        {
            await _client.DeleteAsync(action.GroupId, action.ActivityId, action.ExpenseId);
            dispatcher.Dispatch(new DeleteExpenseSuccessAction(action.ExpenseId));
            // Refresh list and re-compute live balance.
            dispatcher.Dispatch(new LoadExpensesAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadBalancesAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete expense {ExpenseId}", action.ExpenseId);
            dispatcher.Dispatch(new DeleteExpenseFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
