using Fluxor;
using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Family;

public class FamilyEffects
{
    private readonly IFamilyClient _client;

    public FamilyEffects(IFamilyClient client)
    {
        _client = client;
    }

    [EffectMethod(typeof(LoadMyFamilyAction))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        try
        {
            var family = await _client.GetMyFamilyAsync();
            dispatcher.Dispatch(new LoadMyFamilySuccessAction(family));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new LoadMyFamilyFailureAction(ex.Message));
        }
    }

    [EffectMethod]
    public async Task HandleRename(UpdateFamilyNameAction action, IDispatcher dispatcher)
    {
        try
        {
            var family = await _client.UpdateFamilyNameAsync(action.Request);
            dispatcher.Dispatch(new UpdateFamilyNameSuccessAction(family));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new UpdateFamilyNameFailureAction(ex.Message));
        }
    }

    [EffectMethod]
    public async Task HandleAddMember(AddFamilyMemberAction action, IDispatcher dispatcher)
    {
        try
        {
            var member = await _client.AddMemberAsync(action.Request);
            dispatcher.Dispatch(new AddFamilyMemberSuccessAction(member));
            // Reload the full family to reflect the new member.
            dispatcher.Dispatch(new LoadMyFamilyAction());
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new AddFamilyMemberFailureAction(ex.Message));
        }
    }

    [EffectMethod]
    public async Task HandleUpdateMember(UpdateFamilyMemberAction action, IDispatcher dispatcher)
    {
        try
        {
            var member = await _client.UpdateMemberAsync(action.MemberId, action.Request);
            dispatcher.Dispatch(new UpdateFamilyMemberSuccessAction(member));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new UpdateFamilyMemberFailureAction(ex.Message));
        }
    }

    [EffectMethod]
    public async Task HandleRemoveMember(RemoveFamilyMemberAction action, IDispatcher dispatcher)
    {
        try
        {
            await _client.RemoveMemberAsync(action.MemberId);
            dispatcher.Dispatch(new RemoveFamilyMemberSuccessAction(action.MemberId));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new RemoveFamilyMemberFailureAction(ex.Message));
        }
    }
}
