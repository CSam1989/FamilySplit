using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Family;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.Family;

public class FamilyReducersTests
{
    private static FamilyDto CreateFamilyDto(string name = "Test Family") =>
        new(Guid.NewGuid(), name, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new FamilyState { IsLoading = false, ErrorMessage = "old error" };

        var result = FamilyReducers.OnLoad(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsFamily_AndStopsLoading()
    {
        var state = new FamilyState { IsLoading = true };
        var family = CreateFamilyDto();

        var result = FamilyReducers.OnLoadSuccess(state, new LoadMyFamilySuccessAction(family));

        result.IsLoading.Should().BeFalse();
        result.MyFamily.Should().BeSameAs(family);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnLoadFailure(state, new LoadMyFamilyFailureAction("fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }

    [Fact]
    public void OnRename_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new FamilyState { IsLoading = false, ErrorMessage = "err" };

        var result = FamilyReducers.OnRename(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnRenameSuccess_StopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnRenameSuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnRenameFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnRenameFailure(state, new UpdateFamilyNameFailureAction("rename failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("rename failed");
    }

    [Fact]
    public void OnAddMember_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new FamilyState { IsLoading = false, ErrorMessage = "err" };

        var result = FamilyReducers.OnAddMember(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnAddMemberSuccess_StopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnAddMemberSuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnAddMemberFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnAddMemberFailure(state, new AddFamilyMemberFailureAction("add failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("add failed");
    }

    [Fact]
    public void OnUpdateMember_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new FamilyState { IsLoading = false, ErrorMessage = "err" };

        var result = FamilyReducers.OnUpdateMember(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnUpdateMemberSuccess_StopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnUpdateMemberSuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnUpdateMemberFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnUpdateMemberFailure(state, new UpdateFamilyMemberFailureAction("update failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("update failed");
    }

    [Fact]
    public void OnRemoveMember_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new FamilyState { IsLoading = false, ErrorMessage = "err" };

        var result = FamilyReducers.OnRemoveMember(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnRemoveMemberSuccess_StopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnRemoveMemberSuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnRemoveMemberFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new FamilyState { IsLoading = true };

        var result = FamilyReducers.OnRemoveMemberFailure(state, new RemoveFamilyMemberFailureAction("remove failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("remove failed");
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage()
    {
        var state = new FamilyState { ErrorMessage = "some error" };

        var result = FamilyReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnClearError_WhenErrorAlreadyNull_RemainsNull()
    {
        var state = new FamilyState { ErrorMessage = null };

        var result = FamilyReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnClearError_PreservesOtherStateProperties()
    {
        var family = CreateFamilyDto();
        var state = new FamilyState { IsLoading = true, MyFamily = family, ErrorMessage = "err" };

        var result = FamilyReducers.OnClearError(state);

        result.IsLoading.Should().BeTrue();
        result.MyFamily.Should().BeSameAs(family);
        result.ErrorMessage.Should().BeNull();
    }
}
