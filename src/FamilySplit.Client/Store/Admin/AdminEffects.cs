using FamilySplit.Client.Services;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Admin;

public class AdminEffects
{
    private readonly IAdminClient _client;
    private readonly ILogger<AdminEffects> _logger;

    public AdminEffects(IAdminClient client, ILogger<AdminEffects> logger)
    {
        _client = client;
        _logger = logger;
    }

    [EffectMethod(typeof(LoadAdminFamiliesAction))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        try
        {
            var families = await _client.ListFamiliesAsync();
            dispatcher.Dispatch(new LoadAdminFamiliesSuccessAction(families));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin families");
            dispatcher.Dispatch(new LoadAdminFamiliesFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleLoadOne(LoadAdminFamilyAction action, IDispatcher dispatcher)
    {
        try
        {
            var family = await _client.GetFamilyAsync(action.FamilyId);
            dispatcher.Dispatch(new LoadAdminFamilySuccessAction(family));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load family {FamilyId}", action.FamilyId);
            dispatcher.Dispatch(new LoadAdminFamilyFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleCreate(CreateAdminFamilyAction action, IDispatcher dispatcher)
    {
        try
        {
            var family = await _client.CreateFamilyAsync(action.Request);
            dispatcher.Dispatch(new CreateAdminFamilySuccessAction(family));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create family");
            dispatcher.Dispatch(new CreateAdminFamilyFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleAddMember(AddAdminMemberAction action, IDispatcher dispatcher)
    {
        try
        {
            await _client.AddMemberAsync(action.FamilyId, action.Request);
            dispatcher.Dispatch(new AddAdminMemberSuccessAction(action.FamilyId));
            dispatcher.Dispatch(new LoadAdminFamilyAction(action.FamilyId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member to family {FamilyId}", action.FamilyId);
            dispatcher.Dispatch(new AddAdminMemberFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleUpdateMember(UpdateAdminMemberAction action, IDispatcher dispatcher)
    {
        try
        {
            var member = await _client.UpdateMemberAsync(action.FamilyId, action.MemberId, action.Request);
            dispatcher.Dispatch(new UpdateAdminMemberSuccessAction(member));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update member {MemberId}", action.MemberId);
            dispatcher.Dispatch(new UpdateAdminMemberFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }

    [EffectMethod]
    public async Task HandleRemoveMember(RemoveAdminMemberAction action, IDispatcher dispatcher)
    {
        try
        {
            await _client.RemoveMemberAsync(action.FamilyId, action.MemberId);
            dispatcher.Dispatch(new RemoveAdminMemberSuccessAction(action.FamilyId, action.MemberId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove member {MemberId}", action.MemberId);
            dispatcher.Dispatch(new RemoveAdminMemberFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
