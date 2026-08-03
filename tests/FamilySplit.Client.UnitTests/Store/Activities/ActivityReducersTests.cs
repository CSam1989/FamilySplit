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

    // Strict CQRS: the success reducer just stops loading; the effect re-queries the list/detail.
    [Fact]
    public void OnCreateSuccess_StopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCreateSuccess(state);

        result.IsLoading.Should().BeFalse();
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

    // Strict CQRS: the effect re-queries the parent's detail, which repopulates the sub list;
    // the success reducer just stops loading.
    [Fact]
    public void OnCreateSubSuccess_StopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCreateSubSuccess(state);

        result.IsLoading.Should().BeFalse();
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
    public void OnUpdateSuccess_StopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnUpdateSuccess(state);

        result.IsLoading.Should().BeFalse();
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
    public void OnCloseSuccess_StopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnCloseSuccess(state);

        result.IsLoading.Should().BeFalse();
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
    public void OnAddParticipantSuccess_StopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnAddParticipantSuccess(state);

        result.IsLoading.Should().BeFalse();
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
    public void OnRemoveParticipantSuccess_StopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ActivityReducers.OnRemoveParticipantSuccess(state);

        result.IsLoading.Should().BeFalse();
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
