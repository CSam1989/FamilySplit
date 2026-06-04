using Bunit;
using FamilySplit.Client.Pages.Family;
using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Auth;
using FamilySplit.Client.Store.Family;
using FamilySplit.Client.Store.FamilyMembers;
using FamilySplit.Client.UnitTests.Infrastructure;
using FamilySplit.Domain.Enums;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>
/// bUnit tests for permission-guard rendering in <see cref="ManageFamily"/>.
///
/// Key guards (from CLAUDE.md):
///  • Add member button is visible only when the caller is an admin.
///  • The logged-in user's own remove button is never shown.
/// </summary>
public sealed class PermissionGuardTests : BunitTestContext
{
    // ── Fixture builders ──────────────────────────────────────────────────────

    private static FamilyMemberDto MakeMember(Guid id, bool isAdmin) =>
        new(id, "Test User", "user@test.com", null, null,
            1.0m, WeightTier.Volwassene, true, true, isAdmin,
            DateTimeOffset.UtcNow);

    private static FamilyDto MakeFamily(params FamilyMemberDto[] members) =>
        new(Guid.NewGuid(), "Test Family", members, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    /// <summary>
    /// Registers the Fluxor state mocks and IDialogService so ManageFamily renders.
    /// Returns the caller's member ID for assertions that reference the logged-in user.
    /// </summary>
    private Guid SetupServices(FamilyDto family, Guid callerMemberId)
    {
        var callerProfile = family.Members.First(m => m.Id == callerMemberId);

        // Family + profile state
        var familyStateMock = new Mock<IState<FamilyState>>();
        familyStateMock.Setup(s => s.Value)
            .Returns(new FamilyState { MyFamily = family, IsLoading = false });

        var profileStateMock = new Mock<IState<FamilyMemberState>>();
        profileStateMock.Setup(s => s.Value)
            .Returns(new FamilyMemberState { MyProfile = callerProfile });

        // RequireAuth reads AuthState — must be authenticated or the page redirects away.
        var authStateMock = new Mock<IState<AuthState>>();
        authStateMock.Setup(s => s.Value)
            .Returns(new AuthState { IsAuthenticated = true, IsLoading = false });

        var dispatcherMock = new Mock<IDispatcher>();
        var dialogMock = new Mock<IDialogService>();

        Services.AddSingleton<IState<FamilyState>>(familyStateMock.Object);
        Services.AddSingleton<IState<FamilyMemberState>>(profileStateMock.Object);
        Services.AddSingleton<IState<AuthState>>(authStateMock.Object);
        Services.AddSingleton<IDispatcher>(dispatcherMock.Object);
        Services.AddSingleton<IDialogService>(dialogMock.Object);

        return callerMemberId;
    }

    // ── Add member button ─────────────────────────────────────────────────────

    [Fact]
    public void AddMember_Button_Visible_For_Admin()
    {
        var adminId = Guid.NewGuid();
        var family = MakeFamily(MakeMember(adminId, isAdmin: true));
        SetupServices(family, adminId);

        var cut = Render<ManageFamily>();

        cut.FindAll("[data-testid='btn-add-member']")
           .Should().ContainSingle("an admin sees the Add member button");
    }

    [Fact]
    public void AddMember_Button_Hidden_For_Non_Admin()
    {
        var memberId = Guid.NewGuid();
        var family = MakeFamily(MakeMember(memberId, isAdmin: false));
        SetupServices(family, memberId);

        var cut = Render<ManageFamily>();

        cut.FindAll("[data-testid='btn-add-member']")
           .Should().BeEmpty("a non-admin must not see the Add member button");
    }

    // ── Own remove button ─────────────────────────────────────────────────────

    [Fact]
    public void Remove_Button_Hidden_For_Own_Member_Even_As_Admin()
    {
        var adminId = Guid.NewGuid();
        var family = MakeFamily(MakeMember(adminId, isAdmin: true));
        SetupServices(family, adminId);

        var cut = Render<ManageFamily>();

        // The admin's own remove button must never render.
        cut.FindAll($"[data-testid='btn-remove-member-{adminId}']")
           .Should().BeEmpty("admins cannot remove themselves");
    }

    [Fact]
    public void Remove_Button_Shown_For_Other_Members_When_Caller_Is_Admin()
    {
        var adminId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var family = MakeFamily(
            MakeMember(adminId, isAdmin: true),
            MakeMember(otherId, isAdmin: false));
        SetupServices(family, adminId);

        var cut = Render<ManageFamily>();

        // The admin sees a remove button for the OTHER member.
        cut.FindAll($"[data-testid='btn-remove-member-{otherId}']")
           .Should().ContainSingle("admins can remove other members");

        // But not for themselves.
        cut.FindAll($"[data-testid='btn-remove-member-{adminId}']")
           .Should().BeEmpty("self-removal is always blocked");
    }

    [Fact]
    public void Remove_Button_Hidden_For_All_When_Caller_Is_Not_Admin()
    {
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var family = MakeFamily(
            MakeMember(memberId, isAdmin: false),
            MakeMember(otherId, isAdmin: false));
        SetupServices(family, memberId);

        var cut = Render<ManageFamily>();

        // Non-admin sees no remove buttons at all.
        cut.FindAll("[data-testid^='btn-remove-member-']")
           .Should().BeEmpty("non-admins see no remove buttons");
    }
}
