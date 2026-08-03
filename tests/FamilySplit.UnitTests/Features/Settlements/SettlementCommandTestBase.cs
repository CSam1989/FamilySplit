using FamilySplit.Common.Security;
using FamilySplit.Features.Settlements.Data;
using Moq;

namespace FamilySplit.UnitTests.Features.Settlements;

/// <summary>
/// Shared Moq scaffolding for the Settlements command-handler tests (ADR-001): the command handlers are
/// business logic over the <see cref="ISettlementData"/> seam and <see cref="IGroupMembershipGuard"/>,
/// so they are tested with mocks and <strong>no database</strong>. The <c>SettlementData</c> gateway and
/// the query handlers are verified separately with Testcontainers in <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public abstract class SettlementCommandTestBase
{
    protected readonly Mock<ISettlementData> Data = new();
    protected readonly Mock<IGroupMembershipGuard> Guard = new();

    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    protected readonly Guid GroupId = Guid.NewGuid();
    protected readonly Guid ActivityId = Guid.NewGuid();
    protected readonly Guid SettlementId = Guid.NewGuid();
    protected readonly Guid CallerId = Guid.NewGuid();
    protected readonly Guid CallerFamilyId = Guid.NewGuid();
    protected readonly Guid OtherFamilyId = Guid.NewGuid();

    protected SettlementCommandTestBase()
    {
        // The default caller resolves to a stable family id; tests that need a failure override it.
        Guard.Setup(g => g.GetCallerFamilyIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerFamilyId);
    }

    /// <summary>Arrange the group-membership guard to reject the caller (403).</summary>
    protected void ArrangeNotGroupMember() =>
        Guard.Setup(g => g.RequireGroupMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FamilySplit.Common.Exceptions.ForbiddenException());
}
