using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Dashboard;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.Dashboard;

public class DashboardReducersTests
{
    [Fact]
    public void OnLoad_SetsIsLoadingTrueAndClearsError()
    {
        var state = new DashboardState { IsLoading = false, ErrorMessage = "old error" };

        var result = DashboardReducers.OnLoad(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsStatsAndClearsLoading()
    {
        var state = new DashboardState { IsLoading = true };
        var stats = new List<DashboardGroupStatDto> { new(Guid.NewGuid(), "G1", 1, 1, 1, 1, 100m, 50m, 25m, 10m, "EUR", 5m, 2, null, null) };
        var action = new LoadDashboardStatsSuccessAction(stats);

        var result = DashboardReducers.OnLoadSuccess(state, action);

        result.IsLoading.Should().BeFalse();
        result.Stats.Should().BeSameAs(stats);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorAndClearsLoading()
    {
        var state = new DashboardState { IsLoading = true };
        var action = new LoadDashboardStatsFailureAction("fail");

        var result = DashboardReducers.OnLoadFailure(state, action);

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage()
    {
        var state = new DashboardState { ErrorMessage = "some error" };

        var result = DashboardReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
    }
}
