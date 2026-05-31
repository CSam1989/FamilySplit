using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Admin;
using FamilySplit.Domain.Enums;
using FluentAssertions;

namespace FamilySplit.UnitTests.Store.Admin;

public class AdminReducersTests
{
    private static AdminState DefaultState() => new();

    private static FamilyDto CreateFamily(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Test", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new AdminState { IsLoading = false, ErrorMessage = "old" };

        var result = AdminReducers.OnLoad(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsFamilies_AndStopsLoading()
    {
        var families = new List<FamilyDto> { CreateFamily() };
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnLoadSuccess(state, new LoadAdminFamiliesSuccessAction(families));

        result.IsLoading.Should().BeFalse();
        result.Families.Should().BeSameAs(families);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnLoadFailure(state, new LoadAdminFamiliesFailureAction("fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }

    [Fact]
    public void OnLoadOne_SetsIsLoading_ClearsErrorAndSelectedFamily()
    {
        var state = new AdminState { IsLoading = false, ErrorMessage = "err", SelectedFamily = CreateFamily() };

        var result = AdminReducers.OnLoadOne(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SelectedFamily.Should().BeNull();
    }

    [Fact]
    public void OnLoadOneSuccess_SetsSelectedFamily_AndStopsLoading()
    {
        var family = CreateFamily();
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnLoadOneSuccess(state, new LoadAdminFamilySuccessAction(family));

        result.IsLoading.Should().BeFalse();
        result.SelectedFamily.Should().BeSameAs(family);
    }

    [Fact]
    public void OnLoadOneFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnLoadOneFailure(state, new LoadAdminFamilyFailureAction("not found"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("not found");
    }

    [Fact]
    public void OnCreate_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new AdminState { IsLoading = false, ErrorMessage = "old" };

        var result = AdminReducers.OnCreate(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnCreateSuccess_SetsSelectedFamily_AndAppendsFamilyToList()
    {
        var existing = CreateFamily();
        var newFamily = CreateFamily();
        var state = new AdminState { IsLoading = true, Families = [existing] };

        var result = AdminReducers.OnCreateSuccess(state, new CreateAdminFamilySuccessAction(newFamily));

        result.IsLoading.Should().BeFalse();
        result.SelectedFamily.Should().BeSameAs(newFamily);
        result.Families.Should().HaveCount(2);
        result.Families.Should().ContainInOrder(existing, newFamily);
    }

    [Fact]
    public void OnCreateFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnCreateFailure(state, new CreateAdminFamilyFailureAction("create failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("create failed");
    }

    [Fact]
    public void OnAddMember_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new AdminState { IsLoading = false, ErrorMessage = "old" };

        var result = AdminReducers.OnAddMember(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    private static FamilyMemberDto CreateMember(Guid? id = null, string name = "Alice") =>
        new(id ?? Guid.NewGuid(), name, null, null, null, 1.0m, WeightTier.Volwassene, true, false, false, DateTimeOffset.UtcNow);

    [Fact]
    public void OnAddMemberSuccess_StopsLoading()
    {
        var state = new AdminState { IsLoading = true, ErrorMessage = "old" };

        var result = AdminReducers.OnAddMemberSuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnAddMemberFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnAddMemberFailure(state, new AddAdminMemberFailureAction("add failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("add failed");
    }

    [Fact]
    public void OnUpdateMember_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new AdminState { IsLoading = false, ErrorMessage = "old" };

        var result = AdminReducers.OnUpdateMember(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnUpdateMemberSuccess_WhenSelectedFamilyIsNull_ReturnsStateWithLoadingFalse()
    {
        var state = new AdminState { IsLoading = true, SelectedFamily = null };

        var result = AdminReducers.OnUpdateMemberSuccess(state, new UpdateAdminMemberSuccessAction(CreateMember()));

        result.IsLoading.Should().BeFalse();
        result.SelectedFamily.Should().BeNull();
    }

    [Fact]
    public void OnUpdateMemberSuccess_ReplacesMemberInSelectedFamily()
    {
        var memberId = Guid.NewGuid();
        var original = CreateMember(memberId, "Old");
        var updated = CreateMember(memberId, "New");
        var other = CreateMember();
        var family = CreateFamily() with { Members = [original, other] };
        var state = new AdminState { IsLoading = true, SelectedFamily = family };

        var result = AdminReducers.OnUpdateMemberSuccess(state, new UpdateAdminMemberSuccessAction(updated));

        result.IsLoading.Should().BeFalse();
        result.SelectedFamily!.Members.Should().HaveCount(2);
        result.SelectedFamily.Members.Should().Contain(m => m.Id == memberId && m.DisplayName == "New");
        result.SelectedFamily.Members.Should().Contain(m => m.Id == other.Id);
    }

    [Fact]
    public void OnUpdateMemberFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnUpdateMemberFailure(state, new UpdateAdminMemberFailureAction("update failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("update failed");
    }

    [Fact]
    public void OnRemoveMember_SetsIsLoadingTrue_AndClearsError()
    {
        var state = new AdminState { IsLoading = false, ErrorMessage = "old" };

        var result = AdminReducers.OnRemoveMember(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnRemoveMemberSuccess_WhenSelectedFamilyIsNull_ReturnsStateWithLoadingFalse()
    {
        var state = new AdminState { IsLoading = true, SelectedFamily = null };

        var result = AdminReducers.OnRemoveMemberSuccess(state, new RemoveAdminMemberSuccessAction(Guid.NewGuid(), Guid.NewGuid()));

        result.IsLoading.Should().BeFalse();
        result.SelectedFamily.Should().BeNull();
    }

    [Fact]
    public void OnRemoveMemberSuccess_RemovesMemberFromSelectedFamily()
    {
        var memberId = Guid.NewGuid();
        var member = CreateMember(memberId, "ToRemove");
        var other = CreateMember();
        var family = CreateFamily() with { Members = [member, other] };
        var state = new AdminState { IsLoading = true, SelectedFamily = family };

        var result = AdminReducers.OnRemoveMemberSuccess(state, new RemoveAdminMemberSuccessAction(family.Id, memberId));

        result.IsLoading.Should().BeFalse();
        result.SelectedFamily!.Members.Should().HaveCount(1);
        result.SelectedFamily.Members.Should().NotContain(m => m.Id == memberId);
        result.SelectedFamily.Members.Should().Contain(m => m.Id == other.Id);
    }

    [Fact]
    public void OnRemoveMemberFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = new AdminState { IsLoading = true };

        var result = AdminReducers.OnRemoveMemberFailure(state, new RemoveAdminMemberFailureAction("remove failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("remove failed");
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage()
    {
        var state = new AdminState { ErrorMessage = "some error", IsLoading = true };

        var result = AdminReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
        result.IsLoading.Should().BeTrue();
    }
}
