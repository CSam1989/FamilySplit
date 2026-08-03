using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Activities;
using FamilySplit.Client.Store.Settlements;
using FluentAssertions;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FamilySplit.Client.UnitTests.Store.Activities;

public class ActivityEffectsTests
{
    private readonly Mock<IActivityClient> _client = new();
    private readonly Mock<ILogger<ActivityEffects>> _logger = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly ActivityEffects _sut;

    public ActivityEffectsTests()
    {
        _sut = new ActivityEffects(_client.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        var groupId = Guid.NewGuid();
        var activities = new List<ActivitySummaryDto>();
        _client.Setup(c => c.ListAsync(groupId)).ReturnsAsync(activities);

        await _sut.HandleLoad(new LoadActivitiesAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivitiesSuccessAction>(a => a.Activities == activities)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        _client.Setup(c => c.ListAsync(groupId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoad(new LoadActivitiesAction(groupId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadActivitiesFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Success_DispatchesSuccessAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var detail = CreateDetail();
        _client.Setup(c => c.GetAsync(groupId, activityId)).ReturnsAsync(detail);

        await _sut.HandleLoadDetail(new LoadActivityDetailAction(groupId, activityId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailSuccessAction>(a => a.Activity == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        _client.Setup(c => c.GetAsync(groupId, activityId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoadDetail(new LoadActivityDetailAction(groupId, activityId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadActivityDetailFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Success_DispatchesSuccessAndReloadsList()
    {
        var groupId = Guid.NewGuid();
        var request = new CreateActivityRequest("Test", null);
        var newId = Guid.NewGuid();
        _client.Setup(c => c.CreateAsync(groupId, request)).ReturnsAsync(new CreatedResponse(newId));

        await _sut.HandleCreate(new CreateActivityAction(groupId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CreateActivitySuccessAction>(a => a.ActivityId == newId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivitiesAction>(a => a.GroupId == groupId)), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var request = new CreateActivityRequest("Test", null);
        _client.Setup(c => c.CreateAsync(groupId, request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleCreate(new CreateActivityAction(groupId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateActivityFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateActivitySuccessAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleCreateSub_Success_DispatchesSuccessAndReloadsParentDetail()
    {
        var groupId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var request = new CreateActivityRequest("Sub", null);
        var newId = Guid.NewGuid();
        _client.Setup(c => c.CreateSubActivityAsync(groupId, parentId, request)).ReturnsAsync(new CreatedResponse(newId));

        await _sut.HandleCreateSub(new CreateSubActivityAction(groupId, parentId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CreateSubActivitySuccessAction>(a => a.ActivityId == newId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == groupId && a.ActivityId == parentId)), Times.Once);
    }

    [Fact]
    public async Task HandleCreateSub_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var request = new CreateActivityRequest("Sub", null);
        _client.Setup(c => c.CreateSubActivityAsync(groupId, parentId, request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleCreateSub(new CreateSubActivityAction(groupId, parentId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateSubActivityFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateSubActivitySuccessAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpdate_Success_DispatchesSuccessAndReloadsDetail()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var request = new UpdateActivityRequest("Updated", "desc");
        _client.Setup(c => c.UpdateAsync(groupId, activityId, request)).Returns(Task.CompletedTask);

        await _sut.HandleUpdate(new UpdateActivityAction(groupId, activityId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<UpdateActivitySuccessAction>(a => a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
    }

    [Fact]
    public async Task HandleUpdate_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var request = new UpdateActivityRequest("Updated", null);
        _client.Setup(c => c.UpdateAsync(groupId, activityId, request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleUpdate(new UpdateActivityAction(groupId, activityId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdateActivityFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadActivityDetailAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleClose_Success_DispatchesSuccessAndRelatedActions()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        _client.Setup(c => c.CloseAsync(groupId, activityId)).Returns(Task.CompletedTask);

        await _sut.HandleClose(new CloseActivityAction(groupId, activityId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CloseActivitySuccessAction>(a => a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivitiesAction>(a => a.GroupId == groupId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<GenerateSettlementsAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
    }

    [Fact]
    public async Task HandleClose_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        _client.Setup(c => c.CloseAsync(groupId, activityId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleClose(new CloseActivityAction(groupId, activityId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CloseActivityFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<GenerateSettlementsAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleAddParticipant_Success_DispatchesSuccessReloadsDetailAndBalances()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var request = new AddParticipantRequest(Guid.NewGuid());
        _client.Setup(c => c.AddParticipantAsync(groupId, activityId, request)).Returns(Task.CompletedTask);

        await _sut.HandleAddParticipant(new AddParticipantAction(groupId, activityId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<AddParticipantSuccessAction>(a => a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
    }

    [Fact]
    public async Task HandleAddParticipant_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var request = new AddParticipantRequest(Guid.NewGuid());
        _client.Setup(c => c.AddParticipantAsync(groupId, activityId, request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleAddParticipant(new AddParticipantAction(groupId, activityId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<AddParticipantFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadActivityDetailAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleRemoveParticipant_Success_DispatchesSuccessReloadsDetailAndBalances()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _client.Setup(c => c.RemoveParticipantAsync(groupId, activityId, memberId)).Returns(Task.CompletedTask);

        await _sut.HandleRemoveParticipant(new RemoveParticipantAction(groupId, activityId, memberId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<RemoveParticipantSuccessAction>(a => a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesAction>(a => a.GroupId == groupId && a.ActivityId == activityId)), Times.Once);
    }

    [Fact]
    public async Task HandleRemoveParticipant_Failure_DispatchesFailureAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _client.Setup(c => c.RemoveParticipantAsync(groupId, activityId, memberId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleRemoveParticipant(new RemoveParticipantAction(groupId, activityId, memberId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<RemoveParticipantFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadActivityDetailAction>()), Times.Never);
    }

    private static ActivityDetailDto CreateDetail() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Test", null, Domain.Enums.ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
}
