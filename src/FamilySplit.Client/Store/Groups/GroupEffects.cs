using Fluxor;
using FamilySplit.Client.Services;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Groups;

public class GroupEffects
{
    private readonly IGroupClient _client;
    private readonly ILogger<GroupEffects> _logger;

    public GroupEffects(IGroupClient client, ILogger<GroupEffects> logger)
    {
        _client = client;
        _logger = logger;
    }

    [EffectMethod(typeof(LoadGroupsAction))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        try
        {
            var groups = await _client.ListAsync();
            dispatcher.Dispatch(new LoadGroupsSuccessAction(groups));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load groups");
            dispatcher.Dispatch(new LoadGroupsFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoadDetail(LoadGroupDetailAction action, IDispatcher dispatcher)
    {
        try
        {
            var group = await _client.GetAsync(action.GroupId);
            dispatcher.Dispatch(new LoadGroupDetailSuccessAction(group));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new LoadGroupDetailFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleCreate(CreateGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            var group = await _client.CreateAsync(action.Request);
            dispatcher.Dispatch(new CreateGroupSuccessAction(group));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create group");
            dispatcher.Dispatch(new CreateGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleUpdate(UpdateGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            var group = await _client.UpdateAsync(action.GroupId, action.Request);
            dispatcher.Dispatch(new UpdateGroupSuccessAction(group));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new UpdateGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleJoin(JoinGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            var group = await _client.JoinAsync(action.Request);
            dispatcher.Dispatch(new JoinGroupSuccessAction(group));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join group");
            dispatcher.Dispatch(new JoinGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleRegenerateInviteCode(RegenerateInviteCodeAction action, IDispatcher dispatcher)
    {
        try
        {
            var response = await _client.RegenerateInviteCodeAsync(action.GroupId);
            dispatcher.Dispatch(new RegenerateInviteCodeSuccessAction(action.GroupId, response.InviteCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate invite code for group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new RegenerateInviteCodeFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
