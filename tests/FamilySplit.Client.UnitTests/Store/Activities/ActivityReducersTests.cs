using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Activities;
using FamilySplit.Domain.Enums;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.Activities;

public class ActivityReducersTests
{
    private readonly ActivityState _initialState = new()
    {
        IsLoading = false,
        ErrorMessage = "old error",
        Activities = [],
        SelectedActivity = null,
    };

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnLoad(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsActivities_AndStopsLoading()
    {
        var activities = new List<ActivitySummaryDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Test", null, ActivityStatus.Open, null, 2, 0, DateTimeOffset.UtcNow, null, 0, 0m, "EUR"),
        };
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnLoadSuccess(state, new LoadActivitiesSuccessAction(activities));

        result.IsLoading.Should().BeFalse();
        result.Activities.Should().BeSameAs(activities);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnLoadFailure(state, new LoadActivitiesFailureAction("fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }

    [Fact]
    public void OnLoadDetail_SetsIsLoadingTrue_ClearsErrorAndSelectedActivity()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "A", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { SelectedActivity = detail };

        var result = ActivityReducers.OnLoadDetail(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SelectedActivity.Should().BeNull();
    }

    [Fact]
    public void OnLoadDetailSuccess_SetsSelectedActivity_AndStopsLoading()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "A", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnLoadDetailSuccess(state, new LoadActivityDetailSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnLoadDetailFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnLoadDetailFailure(state, new LoadActivityDetailFailureAction("detail fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("detail fail");
    }

    [Fact]
    public void OnCreate_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnCreate(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnCreateSuccess_SetsSelectedActivity_AndStopsLoading()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "A", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCreateSuccess(state, new CreateActivitySuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnCreateFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCreateFailure(state, new CreateActivityFailureAction("create fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("create fail");
    }

    [Fact]
    public void OnCreateSub_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnCreateSub(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnCreateSubSuccess_WhenSelectedActivityIsNull_StopsLoadingOnly()
    {
        var state = _initialState with { IsLoading = true, SelectedActivity = null };
        var subDetail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "Sub", null, ActivityStatus.Open, Guid.NewGuid(), [], [], DateTimeOffset.UtcNow, null);

        var result = ActivityReducers.OnCreateSubSuccess(state, new CreateSubActivitySuccessAction(subDetail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeNull();
    }

    [Fact]
    public void OnCreateSubSuccess_WhenSelectedActivityExists_AppendsSubActivity()
    {
        var parentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var parent = new ActivityDetailDto(parentId, groupId, "Parent", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true, SelectedActivity = parent };

        var subId = Guid.NewGuid();
        var participants = new List<ActivityParticipantDto> { new(Guid.NewGuid(), Guid.NewGuid(), "Alice", Guid.NewGuid(), "Family", 1.0m, WeightTier.Volwassene) };
        var subDetail = new ActivityDetailDto(subId, groupId, "Sub", "desc", ActivityStatus.Open, parentId, participants, [], DateTimeOffset.UtcNow, null);

        var result = ActivityReducers.OnCreateSubSuccess(state, new CreateSubActivitySuccessAction(subDetail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().NotBeNull();
        result.SelectedActivity!.SubActivities.Should().HaveCount(1);
        result.SelectedActivity.SubActivities[0].Id.Should().Be(subId);
        result.SelectedActivity.SubActivities[0].Name.Should().Be("Sub");
        result.SelectedActivity.SubActivities[0].ParticipantCount.Should().Be(1);
    }

    [Fact]
    public void OnCreateSubSuccess_AppendsToExistingSubActivities()
    {
        var parentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var existingSub = new ActivitySummaryDto(Guid.NewGuid(), groupId, "Existing", null, ActivityStatus.Open, parentId, 1, 0, DateTimeOffset.UtcNow, null, 0, 0m, "EUR");
        var parent = new ActivityDetailDto(parentId, groupId, "Parent", null, ActivityStatus.Open, null, [], [existingSub], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true, SelectedActivity = parent };

        var newSubDetail = new ActivityDetailDto(Guid.NewGuid(), groupId, "New Sub", null, ActivityStatus.Open, parentId, [], [], DateTimeOffset.UtcNow, null);

        var result = ActivityReducers.OnCreateSubSuccess(state, new CreateSubActivitySuccessAction(newSubDetail));

        result.SelectedActivity!.SubActivities.Should().HaveCount(2);
        result.SelectedActivity.SubActivities[0].Id.Should().Be(existingSub.Id);
        result.SelectedActivity.SubActivities[1].Name.Should().Be("New Sub");
    }

    [Fact]
    public void OnCreateSubFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCreateSubFailure(state, new CreateSubActivityFailureAction("sub fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("sub fail");
    }

    [Fact]
    public void OnUpdate_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnUpdate(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnUpdateSuccess_SetsSelectedActivity_AndStopsLoading()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "Updated", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnUpdateSuccess(state, new UpdateActivitySuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnUpdateFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnUpdateFailure(state, new UpdateActivityFailureAction("update fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("update fail");
    }

    [Fact]
    public void OnClose_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnClose(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnCloseSuccess_SetsSelectedActivity_AndStopsLoading()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "A", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCloseSuccess(state, new CloseActivitySuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnCloseFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCloseFailure(state, new CloseActivityFailureAction("close fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("close fail");
    }

    [Fact]
    public void OnAddParticipant_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnAddParticipant(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnAddParticipantSuccess_SetsSelectedActivity_AndStopsLoading()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "A", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnAddParticipantSuccess(state, new AddParticipantSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnAddParticipantFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnAddParticipantFailure(state, new AddParticipantFailureAction("add fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("add fail");
    }

    [Fact]
    public void OnRemoveParticipant_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ActivityReducers.OnRemoveParticipant(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnRemoveParticipantSuccess_SetsSelectedActivity_AndStopsLoading()
    {
        var detail = new ActivityDetailDto(Guid.NewGuid(), Guid.NewGuid(), "A", null, ActivityStatus.Open, null, [], [], DateTimeOffset.UtcNow, null);
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnRemoveParticipantSuccess(state, new RemoveParticipantSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedActivity.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnRemoveParticipantFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnRemoveParticipantFailure(state, new RemoveParticipantFailureAction("remove fail"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("remove fail");
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage()
    {
        var result = ActivityReducers.OnClearError(_initialState);

        result.ErrorMessage.Should().BeNull();
    }
}
