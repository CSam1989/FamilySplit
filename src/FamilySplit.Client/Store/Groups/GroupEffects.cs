using FamilySplit.Client.Services;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Groups;

public class GroupEffects
{
    private readonly IGroupClient _client;
    private readonly IAdminClient _adminClient;
    private readonly ILogger<GroupEffects> _logger;
    private readonly NavigationManager _nav;

    public GroupEffects(IGroupClient client, IAdminClient adminClient, ILogger<GroupEffects> logger, NavigationManager nav)
    {
        _client = client;
        _adminClient = adminClient;
        _logger = logger;
        _nav = nav;
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

    [EffectMethod]
    public async Task HandleLeave(LeaveGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            await _client.LeaveAsync(action.GroupId);
            dispatcher.Dispatch(new LeaveGroupSuccessAction(action.GroupId));
            _nav.NavigateTo("/groups");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new LeaveGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleDelete(DeleteGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            await _adminClient.DeleteGroupAsync(action.GroupId);
            dispatcher.Dispatch(new DeleteGroupSuccessAction(action.GroupId));
            _nav.NavigateTo("/groups");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete group {GroupId}", action.GroupId);
            dispatcher.Dispatch(new DeleteGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleAdminAddFamily(AdminAddFamilyToGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            await _adminClient.AddFamilyToGroupAsync(action.GroupId, new AdminAddFamilyToGroupRequest(action.FamilyId));
            dispatcher.Dispatch(new AdminAddFamilyToGroupSuccessAction(action.GroupId));
            dispatcher.Dispatch(new LoadGroupDetailAction(action.GroupId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add family {FamilyId} to group {GroupId}", action.FamilyId, action.GroupId);
            dispatcher.Dispatch(new AdminAddFamilyToGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleAdminRemoveFamily(AdminRemoveFamilyFromGroupAction action, IDispatcher dispatcher)
    {
        try
        {
            await _adminClient.RemoveFamilyFromGroupAsync(action.GroupId, action.FamilyId);
            dispatcher.Dispatch(new AdminRemoveFamilyFromGroupSuccessAction(action.GroupId));
            dispatcher.Dispatch(new LoadGroupDetailAction(action.GroupId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove family {FamilyId} from group {GroupId}", action.FamilyId, action.GroupId);
            dispatcher.Dispatch(new AdminRemoveFamilyFromGroupFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
