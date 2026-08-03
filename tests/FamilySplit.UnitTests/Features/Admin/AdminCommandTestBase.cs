using FamilySplit.Features.Admin.Data;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin;

/// <summary>
/// Shared Moq scaffolding for the Admin command-handler tests (ADR-001): the command handlers are
/// business logic over the <see cref="IAdminData"/> seam — including the global-admin gate
/// (<c>IsGlobalAdminAsync</c>) — so they are tested with mocks and <strong>no database</strong>. The
/// <c>AdminData</c> gateway and the query handlers are verified separately with Testcontainers in
/// <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public abstract class AdminCommandTestBase
{
    protected readonly Mock<IAdminData> Data = new();

    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    protected readonly Guid CallerId = Guid.NewGuid();
    protected readonly Guid FamilyId = Guid.NewGuid();
    protected readonly Guid GroupId = Guid.NewGuid();
    protected readonly Guid MemberId = Guid.NewGuid();

    protected AdminCommandTestBase()
    {
        // The caller is a global admin by default; tests that need the 403 path override it.
        Data.Setup(d => d.IsGlobalAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>Arrange the caller as a non-global-admin (the 403 precondition).</summary>
    protected void ArrangeNotGlobalAdmin() =>
        Data.Setup(d => d.IsGlobalAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
}
