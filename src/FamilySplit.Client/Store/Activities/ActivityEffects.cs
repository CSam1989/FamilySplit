using Fluxor;
using FamilySplit.Client.Services;
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
            var activity = await _client.CreateAsync(action.GroupId, action.Request);
            dispatcher.Dispatch(new CreateActivitySuccessAction(activity));
            // Refresh the group-level list so the new activity appears immediately.
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
            var activity = await _client.CreateSubActivityAsync(action.GroupId, action.ParentActivityId, action.Request);
            dispatcher.Dispatch(new CreateSubActivitySuccessAction(activity));
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
            var activity = await _client.UpdateAsync(action.GroupId, action.ActivityId, action.Request);
            dispatcher.Dispatch(new UpdateActivitySuccessAction(activity));
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
            var activity = await _client.CloseAsync(action.GroupId, action.ActivityId);
            dispatcher.Dispatch(new CloseActivitySuccessAction(activity));
            // Reload the list so the group detail's activity section reflects the closed status.
            dispatcher.Dispatch(new LoadActivitiesAction(action.GroupId));
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
            var activity = await _client.AddParticipantAsync(action.GroupId, action.ActivityId, action.Request);
            dispatcher.Dispatch(new AddParticipantSuccessAction(activity));
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
            var activity = await _client.RemoveParticipantAsync(action.GroupId, action.ActivityId, action.FamilyMemberId);
            dispatcher.Dispatch(new RemoveParticipantSuccessAction(activity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove participant from activity {ActivityId}", action.ActivityId);
            dispatcher.Dispatch(new RemoveParticipantFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
