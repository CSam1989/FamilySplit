using FamilySplit.Features.Families.Data;
using Moq;

namespace FamilySplit.UnitTests.Features.Families;

/// <summary>
/// Shared Moq scaffolding for the Families command-handler tests (ADR-001): the command handlers are
/// business logic over the <see cref="IFamilyData"/> seam — including resolving the caller's own
/// FamilyMember — so they are tested with mocks and <strong>no database</strong>. The <c>FamilyData</c>
/// gateway and the query handlers are verified separately with Testcontainers in
/// <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public abstract class FamiliesCommandTestBase
{
    protected readonly Mock<IFamilyData> Data = new();

    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    protected readonly Guid CallerId = Guid.NewGuid();
    protected readonly Guid FamilyId = Guid.NewGuid();
    protected readonly Guid CallerMemberId = Guid.NewGuid();
    protected readonly Guid MemberId = Guid.NewGuid();

    protected FamiliesCommandTestBase()
    {
        // The caller is a family admin by default; tests that need the 403 path override it.
        ArrangeCaller(isAdmin: true);
    }

    /// <summary>Arrange the caller as their own active FamilyMember with the given admin flag.</summary>
    protected void ArrangeCaller(bool isAdmin) =>
        Data.Setup(d => d.GetCallerMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FamilyCallerMember(CallerMemberId, FamilyId, isAdmin));

    /// <summary>Arrange the caller as having no linked (active) FamilyMember (the 403 precondition).</summary>
    protected void ArrangeNoCallerMember() =>
        Data.Setup(d => d.GetCallerMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FamilyCallerMember?)null);
}
