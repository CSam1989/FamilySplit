using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Shared;

namespace FamilySplit.UnitTests.Features.Activities.Shared;

public class ActivityCloseGuardTests
{
    [Theory]
    [InlineData(ActivityStatus.Open, true)]
    [InlineData(ActivityStatus.Closed, false)]
    [InlineData(ActivityStatus.Settled, false)]
    [InlineData(ActivityStatus.AbsorbedByParent, false)]
    public void CanClose_OnlyTrueForOpen(ActivityStatus status, bool expected)
    {
        ActivityCloseGuard.CanClose(status).Should().Be(expected);
    }

    [Fact]
    public void IsTopLevel_NullParent_ReturnsTrue()
    {
        ActivityCloseGuard.IsTopLevel(null).Should().BeTrue();
    }

    [Fact]
    public void IsTopLevel_NonNullParent_ReturnsFalse()
    {
        ActivityCloseGuard.IsTopLevel(Guid.NewGuid()).Should().BeFalse();
    }
}
