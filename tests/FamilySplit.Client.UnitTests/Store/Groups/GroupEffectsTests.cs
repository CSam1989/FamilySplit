using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Groups;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FamilySplit.Client.UnitTests.Store.Groups;

public class GroupEffectsTests
{
    private readonly Mock<IGroupClient> _client = new();
    private readonly Mock<IAdminClient> _adminClient = new();
    private readonly Mock<ILogger<GroupEffects>> _logger = new();
    private readonly Mock<NavigationManager> _nav = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly GroupEffects _sut;

    public GroupEffectsTests()
    {
        _sut = new GroupEffects(_client.Object, _adminClient.Object, _logger.Object, _nav.Object);
    }

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        var groups = new List<GroupSummaryDto>();
        _client.Setup(c => c.ListAsync()).ReturnsAsync(groups);

        await _sut.HandleLoad(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadGroupsSuccessAction>(a => a.Groups == groups)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_Failure_DispatchesFailureAction()
    {
        _client.Setup(c => c.ListAsync()).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoad(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadGroupsFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Success_DispatchesSuccessAction()
    {
        var groupId = Guid.NewGuid();
        var detail = CreateDetail();
        _client.Setup(c => c.GetAsync(groupId)).ReturnsAsync(detail);

        await _sut.HandleLoadDetail(new LoadGroupDetailAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadGroupDetailSuccessAction>(a => a.Group == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        _client.Setup(c => c.GetAsync(groupId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoadDetail(new LoadGroupDetailAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadGroupDetailFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Success_DispatchesSuccessAction()
    {
        var request = new CreateGroupRequest("Test", null);
        var detail = CreateDetail();
        _client.Setup(c => c.CreateAsync(request)).ReturnsAsync(detail);

        await _sut.HandleCreate(new CreateGroupAction(request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CreateGroupSuccessAction>(a => a.Group == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Failure_DispatchesFailureAction()
    {
        var request = new CreateGroupRequest("Test", null);
        _client.Setup(c => c.CreateAsync(request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleCreate(new CreateGroupAction(request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateGroupFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdate_Success_DispatchesSuccessAction()
    {
        var groupId = Guid.NewGuid();
        var request = new UpdateGroupRequest("Updated", "desc");
        var detail = CreateDetail();
        _client.Setup(c => c.UpdateAsync(groupId, request)).ReturnsAsync(detail);

        await _sut.HandleUpdate(new UpdateGroupAction(groupId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<UpdateGroupSuccessAction>(a => a.Group == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleUpdate_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var request = new UpdateGroupRequest("Updated", "desc");
        _client.Setup(c => c.UpdateAsync(groupId, request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleUpdate(new UpdateGroupAction(groupId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdateGroupFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleJoin_Success_DispatchesSuccessAction()
    {
        var request = new JoinGroupRequest("INVITE");
        var detail = CreateDetail();
        _client.Setup(c => c.JoinAsync(request)).ReturnsAsync(detail);

        await _sut.HandleJoin(new JoinGroupAction(request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<JoinGroupSuccessAction>(a => a.Group == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleJoin_Failure_DispatchesFailureAction()
    {
        var request = new JoinGroupRequest("INVITE");
        _client.Setup(c => c.JoinAsync(request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleJoin(new JoinGroupAction(request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<JoinGroupFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegenerateInviteCode_Success_DispatchesSuccessAction()
    {
        var groupId = Guid.NewGuid();
        _client.Setup(c => c.RegenerateInviteCodeAsync(groupId)).ReturnsAsync(new RegenerateInviteCodeResponse("NEW123"));

        await _sut.HandleRegenerateInviteCode(new RegenerateInviteCodeAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<RegenerateInviteCodeSuccessAction>(a => a.GroupId == groupId && a.NewInviteCode == "NEW123")), Times.Once);
    }

    [Fact]
    public async Task HandleRegenerateInviteCode_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        _client.Setup(c => c.RegenerateInviteCodeAsync(groupId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleRegenerateInviteCode(new RegenerateInviteCodeAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<RegenerateInviteCodeFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleLeave_Success_DispatchesSuccessAndNavigates()
    {
        var groupId = Guid.NewGuid();
        _client.Setup(c => c.LeaveAsync(groupId)).Returns(Task.CompletedTask);

        await _sut.HandleLeave(new LeaveGroupAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LeaveGroupSuccessAction>(a => a.GroupId == groupId)), Times.Once);
    }

    [Fact]
    public async Task HandleLeave_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        _client.Setup(c => c.LeaveAsync(groupId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLeave(new LeaveGroupAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LeaveGroupFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleDelete_Success_DispatchesSuccessAndNavigates()
    {
        var groupId = Guid.NewGuid();
        _adminClient.Setup(c => c.DeleteGroupAsync(groupId)).Returns(Task.CompletedTask);

        await _sut.HandleDelete(new DeleteGroupAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<DeleteGroupSuccessAction>(a => a.GroupId == groupId)), Times.Once);
    }

    [Fact]
    public async Task HandleDelete_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        _adminClient.Setup(c => c.DeleteGroupAsync(groupId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleDelete(new DeleteGroupAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<DeleteGroupFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleAdminAddFamily_Success_DispatchesSuccessAndLoadDetail()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        _adminClient.Setup(c => c.AddFamilyToGroupAsync(groupId, It.Is<AdminAddFamilyToGroupRequest>(r => r.FamilyId == familyId))).Returns(Task.CompletedTask);

        await _sut.HandleAdminAddFamily(new AdminAddFamilyToGroupAction(groupId, familyId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<AdminAddFamilyToGroupSuccessAction>(a => a.GroupId == groupId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadGroupDetailAction>(a => a.GroupId == groupId)), Times.Once);
    }

    [Fact]
    public async Task HandleAdminAddFamily_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        _adminClient.Setup(c => c.AddFamilyToGroupAsync(groupId, It.IsAny<AdminAddFamilyToGroupRequest>())).ThrowsAsync(new Exception("fail"));

        await _sut.HandleAdminAddFamily(new AdminAddFamilyToGroupAction(groupId, familyId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<AdminAddFamilyToGroupFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleAdminRemoveFamily_Success_DispatchesSuccessAndLoadDetail()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        _adminClient.Setup(c => c.RemoveFamilyFromGroupAsync(groupId, familyId)).Returns(Task.CompletedTask);

        await _sut.HandleAdminRemoveFamily(new AdminRemoveFamilyFromGroupAction(groupId, familyId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<AdminRemoveFamilyFromGroupSuccessAction>(a => a.GroupId == groupId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadGroupDetailAction>(a => a.GroupId == groupId)), Times.Once);
    }

    [Fact]
    public async Task HandleAdminRemoveFamily_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        _adminClient.Setup(c => c.RemoveFamilyFromGroupAsync(groupId, familyId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleAdminRemoveFamily(new AdminRemoveFamilyFromGroupAction(groupId, familyId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<AdminRemoveFamilyFromGroupFailureAction>()), Times.Once);
    }

    private static GroupDetailDto CreateDetail() =>
        new(Guid.NewGuid(), "Test", null, "ABC123", MemberRole.Admin, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
