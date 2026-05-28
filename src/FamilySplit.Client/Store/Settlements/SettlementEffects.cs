using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Activities;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Settlements;

public class SettlementEffects
{
    private readonly ISettlementClient _client;
    private readonly ILogger<SettlementEffects> _logger;

    public SettlementEffects(ISettlementClient client, ILogger<SettlementEffects> logger)
    {
        _client = client;
        _logger = logger;
    }

    [EffectMethod]
    public async Task HandleLoadBalances(LoadBalancesAction action, IDispatcher dispatcher)
    {
        try
        {
            var balances = await _client.GetBalancesAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new LoadBalancesSuccessAction(balances));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load balances for activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new LoadBalancesFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoad(LoadSettlementsAction action, IDispatcher dispatcher)
    {
        try
        {
            var settlements = await _client.ListAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new LoadSettlementsSuccessAction(settlements));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settlements for activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new LoadSettlementsFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleGenerate(GenerateSettlementsAction action, IDispatcher dispatcher)
    {
        try
        {
            var settlements = await _client.GenerateAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new GenerateSettlementsSuccessAction(settlements));
            // Refresh the activity so its status (possibly now Settled) is up to date.
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate settlements for activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new GenerateSettlementsFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoadDetail(LoadSettlementDetailAction action, IDispatcher dispatcher)
    {
        try
        {
            var settlement = await _client.GetDetailAsync(action.GroupId, action.ActivityId, action.SettlementId);
            dispatcher.Dispatch(new LoadSettlementDetailSuccessAction(settlement));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settlement {SettlementId}", action.SettlementId);
            dispatcher.Dispatch(new LoadSettlementDetailFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleConfirmSent(ConfirmSentAction action, IDispatcher dispatcher)
    {
        try
        {
            var settlement = await _client.ConfirmSentAsync(action.GroupId, action.ActivityId, action.SettlementId);
            dispatcher.Dispatch(new ConfirmSentSuccessAction(settlement));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm sent for settlement {SettlementId}", action.SettlementId);
            dispatcher.Dispatch(new ConfirmSentFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleConfirmReceived(ConfirmReceivedAction action, IDispatcher dispatcher)
    {
        try
        {
            var settlement = await _client.ConfirmReceivedAsync(action.GroupId, action.ActivityId, action.SettlementId);
            dispatcher.Dispatch(new ConfirmReceivedSuccessAction(settlement));
            // Reload settlements list so statuses update; also reload activity (may become Settled).
            dispatcher.Dispatch(new LoadSettlementsAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ActivityId));
            // Refresh group-level and dashboard lists.
            dispatcher.Dispatch(new LoadGroupSettlementsAction(action.GroupId));
            dispatcher.Dispatch(new LoadMyPendingSettlementsAction());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm received for settlement {SettlementId}", action.SettlementId);
            dispatcher.Dispatch(new ConfirmReceivedFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoadGroupSettlements(LoadGroupSettlementsAction action, IDispatcher dispatcher)
    {
        try
        {
            var settlements = await _client.ListForGroupAsync(action.GroupId);
            dispatcher.Dispatch(new LoadGroupSettlementsSuccessAction(settlements));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load group settlements for {GroupId}", action.GroupId);
            dispatcher.Dispatch(new LoadGroupSettlementsFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod(typeof(LoadMyPendingSettlementsAction))]
    public async Task HandleLoadMyPending(IDispatcher dispatcher)
    {
        try
        {
            var settlements = await _client.ListMyPendingAsync();
            dispatcher.Dispatch(new LoadMyPendingSettlementsSuccessAction(settlements));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending settlements for dashboard");
            dispatcher.Dispatch(new LoadMyPendingSettlementsFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
