using FamilySplit.Client.Services;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.FamilyMembers;

public class FamilyMemberEffects
{
    private readonly IFamilyMemberClient _client;
    private readonly ILogger<FamilyMemberEffects> _logger;

    public FamilyMemberEffects(IFamilyMemberClient client, ILogger<FamilyMemberEffects> logger)
    {
        _client = client;
        _logger = logger;
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
            _logger.LogError(ex, "Failed to load member profile");
            dispatcher.Dispatch(new LoadMyProfileFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
