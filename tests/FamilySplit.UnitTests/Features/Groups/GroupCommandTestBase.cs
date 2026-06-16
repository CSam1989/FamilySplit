using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using Moq;

namespace FamilySplit.UnitTests.Features.Groups;

/// <summary>
/// Shared Moq scaffolding for the Groups command-handler tests (ADR-001): the command handlers are
/// business logic over the <see cref="IGroupData"/> seam and <see cref="IGroupMembershipGuard"/>, so
/// they are tested with mocks and <strong>no database</strong>. The <c>GroupData</c> gateway and the
/// query handlers are verified separately with Testcontainers in <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public abstract class GroupCommandTestBase
{
    protected readonly Mock<IGroupData> Data = new();
    protected readonly Mock<IGroupMembershipGuard> Guard = new();

    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    protected readonly Guid GroupId = Guid.NewGuid();
    protected readonly Guid CallerId = Guid.NewGuid();
    protected readonly Guid CallerFamilyId = Guid.NewGuid();

    protected GroupCommandTestBase()
    {
        // The default caller resolves to a stable family id; tests that need a failure override it.
        Guard.Setup(g => g.GetCallerFamilyIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerFamilyId);

        // Unique invite-code generation returns a fixed 8-char code unless a test overrides it.
        Data.Setup(d => d.GenerateUniqueInviteCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NEWCODE8");
    }

    /// <summary>Arrange the caller as an active family admin (the create/join/leave precondition).</summary>
    protected void ArrangeCallerIsFamilyAdmin(bool isAdmin = true) =>
        Data.Setup(d => d.IsActiveFamilyAdminAsync(CallerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isAdmin);

    /// <summary>Arrange the caller-family's role within <see cref="GroupId"/> (the update/regenerate precondition).</summary>
    protected void ArrangeCallerRoleInGroup(MemberRole? role) =>
        Data.Setup(d => d.GetFamilyRoleInGroupAsync(GroupId, CallerFamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
}
