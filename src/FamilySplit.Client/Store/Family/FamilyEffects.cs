using FamilySplit.Client.Services;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Family;

public class FamilyEffects
{
    private readonly IFamilyClient _client;
    private readonly ILogger<FamilyEffects> _logger;

    public FamilyEffects(IFamilyClient client, ILogger<FamilyEffects> logger)
    {
        _client = client;
        _logger = logger;
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
            _logger.LogError(ex, "Failed to load family");
            dispatcher.Dispatch(new LoadMyFamilyFailureAction(ErrorHelper.GetMessage(ex)));
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
            _logger.LogError(ex, "Failed to rename family");
            dispatcher.Dispatch(new UpdateFamilyNameFailureAction(ErrorHelper.GetMessage(ex)));
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
            _logger.LogError(ex, "Failed to add family member");
            dispatcher.Dispatch(new AddFamilyMemberFailureAction(ErrorHelper.GetMessage(ex)));
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
            _logger.LogError(ex, "Failed to update member {MemberId}", action.MemberId);
            dispatcher.Dispatch(new UpdateFamilyMemberFailureAction(ErrorHelper.GetMessage(ex)));
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
            _logger.LogError(ex, "Failed to remove member {MemberId}", action.MemberId);
            dispatcher.Dispatch(new RemoveFamilyMemberFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
