using FamilySplit.Client.Services;
using FamilySplit.Client.Store.FamilyMembers;
using FamilySplit.Domain.Enums;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.FamilyMembers;

public class FamilyMemberReducersTests
{
    private static FamilyMemberDto CreateProfile() =>
        new(Guid.NewGuid(), "Test", "test@test.com", null, null, 1.0m, WeightTier.Volwassene, true, true, false, DateTimeOffset.UtcNow);

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_And_ClearsError()
    {
        var state = new FamilyMemberState { IsLoading = false, ErrorMessage = "old error" };

        var result = FamilyMemberReducers.OnLoad(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsProfile_And_ClearsLoading()
    {
        var state = new FamilyMemberState { IsLoading = true };
        var profile = CreateProfile();

        var result = FamilyMemberReducers.OnLoadSuccess(state, new LoadMyProfileSuccessAction(profile));

        result.IsLoading.Should().BeFalse();
        result.MyProfile.Should().Be(profile);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorMessage_And_ClearsLoading()
    {
        var state = new FamilyMemberState { IsLoading = true };

        var result = FamilyMemberReducers.OnLoadFailure(state, new LoadMyProfileFailureAction("fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }
}
