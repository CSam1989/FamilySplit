using Fluxor;
using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.FamilyMembers;

public class FamilyMemberEffects
{
    private readonly IFamilyMemberClient _client;

    public FamilyMemberEffects(IFamilyMemberClient client)
    {
        _client = client;
    }

    [EffectMethod(typeof(LoadMyProfileAction))]
    public async Task HandleLoadProfile(IDispatcher dispatcher)
    {
        try
        {
            var profile = await _client.GetProfileAsync();
            dispatcher.Dispatch(new LoadMyProfileSuccessAction(profile));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new LoadMyProfileFailureAction(ex.Message));
        }
    }
}
