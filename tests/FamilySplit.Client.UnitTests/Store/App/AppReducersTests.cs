using FamilySplit.Client.Store.App;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.App;

public class AppReducersTests
{
    [Fact]
    public void OnSetLastUsedActivity_SetsAllProperties()
    {
        var state = new AppState();
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var action = new SetLastUsedActivityAction(groupId, activityId, "Trip", "Family");

        var result = AppReducers.OnSetLastUsedActivity(state, action);

        result.LastUsedGroupId.Should().Be(groupId);
        result.LastUsedActivityId.Should().Be(activityId);
        result.LastUsedActivityName.Should().Be("Trip");
        result.LastUsedGroupName.Should().Be("Family");
    }

    [Fact]
    public void OnSetLastUsedActivity_PreservesOtherState()
    {
        var state = new AppState { Initialized = true };
        var action = new SetLastUsedActivityAction(Guid.NewGuid(), Guid.NewGuid(), "A", "G");

        var result = AppReducers.OnSetLastUsedActivity(state, action);

        result.Initialized.Should().BeTrue();
    }

    [Fact]
    public void OnSetLastUsedActivity_OverwritesPreviousValues()
    {
        var state = new AppState
        {
            LastUsedGroupId = Guid.NewGuid(),
            LastUsedActivityId = Guid.NewGuid(),
            LastUsedActivityName = "Old",
            LastUsedGroupName = "OldGroup",
        };
        var newGroupId = Guid.NewGuid();
        var newActivityId = Guid.NewGuid();
        var action = new SetLastUsedActivityAction(newGroupId, newActivityId, "New", "NewGroup");

        var result = AppReducers.OnSetLastUsedActivity(state, action);

        result.LastUsedGroupId.Should().Be(newGroupId);
        result.LastUsedActivityId.Should().Be(newActivityId);
        result.LastUsedActivityName.Should().Be("New");
        result.LastUsedGroupName.Should().Be("NewGroup");
    }
}
