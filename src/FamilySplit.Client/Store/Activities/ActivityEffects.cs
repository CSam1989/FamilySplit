using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Settlements;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Activities;

public class ActivityEffects
{
    private readonly IActivityClient _client;
    private readonly ILogger<ActivityEffects> _logger;

    public ActivityEffects(IActivityClient client, ILogger<ActivityEffects> logger)
    {
        _client = client;
        _logger = logger;
    }

    [EffectMethod]
    public async Task HandleLoad(LoadActivitiesAction action, IDispatcher dispatcher)
    {
        try
        {
            var activities = await _client.ListAsync(action.GroupId);
            dispatcher.Dispatch(new LoadActivitiesSuccessAction(activities));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load activities for group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new LoadActivitiesFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoadDetail(LoadActivityDetailAction action, IDispatcher dispatcher)
    {
        try
        {
            var activity = await _client.GetAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new LoadActivityDetailSuccessAction(activity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new LoadActivityDetailFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleCreate(CreateActivityAction action, IDispatcher dispatcher)
    {
        try
        {
            // Strict CQRS: the command returns only the new id; refresh the group-level
            // list so the new activity appears immediately.
            var created = await _client.CreateAsync(action.GroupId, action.Request);
            dispatcher.Dispatch(new CreateActivitySuccessAction(created.Id));
            dispatcher.Dispatch(new LoadActivitiesAction(action.GroupId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create activity in group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new CreateActivityFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleCreateSub(CreateSubActivityAction action, IDispatcher dispatcher)
    {
        try
        {
            // Strict CQRS: the command returns only the new id; re-query the parent's detail
            // so the new sub-activity shows up in its sub list.
            var created = await _client.CreateSubActivityAsync(action.GroupId, action.ParentActivityId, action.Request);
            dispatcher.Dispatch(new CreateSubActivitySuccessAction(created.Id));
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ParentActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sub-activity under {ParentActivityId}", action.ParentActivityId);
            dispatcher.Dispatch(new CreateSubActivityFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleUpdate(UpdateActivityAction action, IDispatcher dispatcher)
    {
        try
        {
            // Strict CQRS: the command returns 204; re-query the detail for the updated data.
            await _client.UpdateAsync(action.GroupId, action.ActivityId, action.Request);
            dispatcher.Dispatch(new UpdateActivitySuccessAction(action.ActivityId));
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new UpdateActivityFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleClose(CloseActivityAction action, IDispatcher dispatcher)
    {
        try
        {
            // Strict CQRS: the command returns 204; re-query the detail to reflect the closed
            // status (and absorbed subs), refresh the list, then generate settlements + balances.
            await _client.CloseAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new CloseActivitySuccessAction(action.ActivityId));
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadActivitiesAction(action.GroupId));
            // Auto-generate settlements and load balances immediately after close.
            dispatcher.Dispatch(new GenerateSettlementsAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadBalancesAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new CloseActivityFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleAddParticipant(AddParticipantAction action, IDispatcher dispatcher)
    {
        try
        {
            // Strict CQRS: the command returns 204; re-query the detail then refresh the balance
            // so weights re-compute correctly.
            await _client.AddParticipantAsync(action.GroupId, action.ActivityId, action.Request);
            dispatcher.Dispatch(new AddParticipantSuccessAction(action.ActivityId));
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadBalancesAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add participant to activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new AddParticipantFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleRemoveParticipant(RemoveParticipantAction action, IDispatcher dispatcher)
    {
        try
        {
            // Strict CQRS: the command returns 204; re-query the detail then refresh the balance.
            await _client.RemoveParticipantAsync(action.GroupId, action.ActivityId, action.FamilyMemberId);
            dispatcher.Dispatch(new RemoveParticipantSuccessAction(action.ActivityId));
            dispatcher.Dispatch(new LoadActivityDetailAction(action.GroupId, action.ActivityId));
            dispatcher.Dispatch(new LoadBalancesAction(action.GroupId, action.ActivityId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove participant from activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new RemoveParticipantFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
