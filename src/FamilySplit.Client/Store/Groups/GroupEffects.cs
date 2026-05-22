using Fluxor;
using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Groups;

public class GroupEffects
{
    private readonly IGroupClient _client;

    public GroupEffects(IGroupClient client)
    {
        _client = client;
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
            dispatcher.Dispatch(new LoadGroupsFailureAction(ex.Message));
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
            dispatcher.Dispatch(new LoadGroupDetailFailureAction(ex.Message));
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
            dispatcher.Dispatch(new CreateGroupFailureAction(ex.Message));
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
            dispatcher.Dispatch(new UpdateGroupFailureAction(ex.Message));
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
            dispatcher.Dispatch(new JoinGroupFailureAction(ex.Message));
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
            dispatcher.Dispatch(new RegenerateInviteCodeFailureAction(ex.Message));
        }
    }
}
