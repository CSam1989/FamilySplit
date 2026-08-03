using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Data;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities;

/// <summary>
/// Shared Moq scaffolding for the Activities command-handler tests (ADR-001): the command handlers
/// are business logic over the <see cref="IActivityData"/> seam and <see cref="IGroupMembershipGuard"/>,
/// so they are tested with mocks and <strong>no database</strong>. The <c>ActivityData</c> gateway and
/// the query handlers are verified separately with Testcontainers in <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public abstract class ActivityCommandTestBase
{
    protected readonly Mock<IActivityData> Data = new();
    protected readonly Mock<IGroupMembershipGuard> Guard = new();

    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    protected readonly Guid GroupId = Guid.NewGuid();
    protected readonly Guid ActivityId = Guid.NewGuid();
    protected readonly Guid CallerId = Guid.NewGuid();

    /// <summary>
    /// Arrange <see cref="IActivityData.GetActivityCoreAsync"/> to return a core record for
    /// <see cref="ActivityId"/> (in <see cref="GroupId"/>) with the given status / parent.
    /// </summary>
    protected void ArrangeActivity(ActivityStatus status = ActivityStatus.Open, Guid? parentId = null) =>
        Data.Setup(d => d.GetActivityCoreAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityCore(ActivityId, GroupId, status, parentId));

    /// <summary>Arrange the membership guard to throw <see cref="FamilySplit.Common.Exceptions.ForbiddenException"/>.</summary>
    protected void ArrangeCallerNotMember() =>
        Guard.Setup(g => g.RequireGroupMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FamilySplit.Common.Exceptions.ForbiddenException());
}
