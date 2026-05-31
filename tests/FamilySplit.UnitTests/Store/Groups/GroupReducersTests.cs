using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Groups;
using FamilySplit.Domain.Enums;
using FluentAssertions;

namespace FamilySplit.UnitTests.Store.Groups;

public class GroupReducersTests
{
    private static readonly GroupState DefaultState = new();

    private static GroupState StateWithError() =>
        DefaultState with { ErrorMessage = "old error", IsLoading = false };

    private static GroupDetailDto CreateDetailDto() =>
        new(Guid.NewGuid(), "G1", null, "INV", MemberRole.Admin, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnLoad(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsGroupsAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };
        var groups = new List<GroupSummaryDto>
        {
            new(Guid.NewGuid(), "G1", null, "INV1", 2, MemberRole.Admin, DateTimeOffset.UtcNow),
        };

        var result = GroupReducers.OnLoadSuccess(state, new LoadGroupsSuccessAction(groups));

        result.IsLoading.Should().BeFalse();
        result.Groups.Should().BeSameAs(groups);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnLoadFailure(state, new LoadGroupsFailureAction("fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }

    [Fact]
    public void OnLoadDetail_SetsLoadingAndClearsSelectedGroupAndError()
    {
        var state = DefaultState with { SelectedGroup = CreateDetailDto(), ErrorMessage = "err" };

        var result = GroupReducers.OnLoadDetail(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SelectedGroup.Should().BeNull();
    }

    [Fact]
    public void OnLoadDetailSuccess_SetsSelectedGroupAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };
        var detail = CreateDetailDto();

        var result = GroupReducers.OnLoadDetailSuccess(state, new LoadGroupDetailSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnLoadDetailFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnLoadDetailFailure(state, new LoadGroupDetailFailureAction("detail fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("detail fail");
    }

    [Fact]
    public void OnCreate_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnCreate(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnCreateSuccess_SetsSelectedGroupAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };
        var detail = CreateDetailDto();

        var result = GroupReducers.OnCreateSuccess(state, new CreateGroupSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnCreateFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnCreateFailure(state, new CreateGroupFailureAction("create fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("create fail");
    }

    [Fact]
    public void OnUpdate_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnUpdate(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnUpdateSuccess_SetsSelectedGroupAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };
        var detail = CreateDetailDto();

        var result = GroupReducers.OnUpdateSuccess(state, new UpdateGroupSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnUpdateFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnUpdateFailure(state, new UpdateGroupFailureAction("update fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("update fail");
    }

    [Fact]
    public void OnJoin_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnJoin(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnJoinSuccess_SetsSelectedGroupAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };
        var detail = CreateDetailDto();

        var result = GroupReducers.OnJoinSuccess(state, new JoinGroupSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnJoinFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnJoinFailure(state, new JoinGroupFailureAction("join fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("join fail");
    }

    [Fact]
    public void OnRegenerate_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnRegenerate(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnRegenerateSuccess_SelectedGroupMatches_UpdatesInviteCode()
    {
        var detail = CreateDetailDto();
        var state = DefaultState with { IsLoading = true, SelectedGroup = detail };

        var result = GroupReducers.OnRegenerateSuccess(state, new RegenerateInviteCodeSuccessAction(detail.Id, "NEW_CODE"));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().NotBeNull();
        result.SelectedGroup!.InviteCode.Should().Be("NEW_CODE");
    }

    [Fact]
    public void OnRegenerateSuccess_SelectedGroupNull_ReturnsStateWithLoadingFalse()
    {
        var state = DefaultState with { IsLoading = true, SelectedGroup = null };

        var result = GroupReducers.OnRegenerateSuccess(state, new RegenerateInviteCodeSuccessAction(Guid.NewGuid(), "NEW"));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeNull();
    }

    [Fact]
    public void OnRegenerateSuccess_SelectedGroupIdMismatch_ReturnsStateWithLoadingFalse()
    {
        var detail = CreateDetailDto();
        var state = DefaultState with { IsLoading = true, SelectedGroup = detail };

        var result = GroupReducers.OnRegenerateSuccess(state, new RegenerateInviteCodeSuccessAction(Guid.NewGuid(), "NEW"));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnRegenerateFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnRegenerateFailure(state, new RegenerateInviteCodeFailureAction("regen fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("regen fail");
    }

    [Fact]
    public void OnLeave_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnLeave(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLeaveSuccess_RemovesGroupAndClearsSelectedGroup()
    {
        var groupId = Guid.NewGuid();
        var groups = new List<GroupSummaryDto>
        {
            new(groupId, "G1", null, "INV1", 2, MemberRole.Admin, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), "G2", null, "INV2", 3, MemberRole.Member, DateTimeOffset.UtcNow),
        };
        var state = DefaultState with { IsLoading = true, Groups = groups, SelectedGroup = CreateDetailDto() };

        var result = GroupReducers.OnLeaveSuccess(state, new LeaveGroupSuccessAction(groupId));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeNull();
        result.Groups.Should().HaveCount(1);
        result.Groups.Should().NotContain(g => g.Id == groupId);
    }

    [Fact]
    public void OnLeaveFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnLeaveFailure(state, new LeaveGroupFailureAction("leave fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("leave fail");
    }

    [Fact]
    public void OnDelete_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnDelete(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnDeleteSuccess_RemovesGroupAndClearsSelectedGroup()
    {
        var groupId = Guid.NewGuid();
        var groups = new List<GroupSummaryDto>
        {
            new(groupId, "G1", null, "INV1", 2, MemberRole.Admin, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), "G2", null, "INV2", 3, MemberRole.Member, DateTimeOffset.UtcNow),
        };
        var state = DefaultState with { IsLoading = true, Groups = groups, SelectedGroup = CreateDetailDto() };

        var result = GroupReducers.OnDeleteSuccess(state, new DeleteGroupSuccessAction(groupId));

        result.IsLoading.Should().BeFalse();
        result.SelectedGroup.Should().BeNull();
        result.Groups.Should().HaveCount(1);
        result.Groups.Should().NotContain(g => g.Id == groupId);
    }

    [Fact]
    public void OnDeleteFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnDeleteFailure(state, new DeleteGroupFailureAction("delete fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("delete fail");
    }

    [Fact]
    public void OnAdminAddFamily_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnAdminAddFamily(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnAdminAddFamilySuccess_ClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnAdminAddFamilySuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnAdminAddFamilyFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnAdminAddFamilyFailure(state, new AdminAddFamilyToGroupFailureAction("add fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("add fail");
    }

    [Fact]
    public void OnAdminRemoveFamily_SetsIsLoadingTrue_AndClearsError()
    {
        var state = StateWithError();

        var result = GroupReducers.OnAdminRemoveFamily(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnAdminRemoveFamilySuccess_ClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnAdminRemoveFamilySuccess(state);

        result.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void OnAdminRemoveFamilyFailure_SetsErrorAndClearsLoading()
    {
        var state = DefaultState with { IsLoading = true };

        var result = GroupReducers.OnAdminRemoveFamilyFailure(state, new AdminRemoveFamilyFromGroupFailureAction("remove fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("remove fail");
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage()
    {
        var state = StateWithError();

        var result = GroupReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnClearError_PreservesOtherState()
    {
        var groups = new List<GroupSummaryDto>
        {
            new(Guid.NewGuid(), "G1", null, "INV1", 2, MemberRole.Admin, DateTimeOffset.UtcNow),
        };
        var state = DefaultState with { ErrorMessage = "err", Groups = groups, IsLoading = true };

        var result = GroupReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
        result.Groups.Should().BeSameAs(groups);
        result.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void OnClearError_WhenNoError_RemainsNull()
    {
        var result = GroupReducers.OnClearError(DefaultState);

        result.ErrorMessage.Should().BeNull();
    }
}
