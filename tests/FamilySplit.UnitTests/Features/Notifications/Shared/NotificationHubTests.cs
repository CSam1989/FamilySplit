using System.Security.Claims;
using FamilySplit.Features.Notifications.Data;
using FamilySplit.Features.Notifications.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Notifications.Shared;

/// <summary>
/// The hub now depends on <see cref="IPushSubscriptionData"/> instead of <c>AppDbContext</c>
/// directly (ADR-001/Rule 5 — "Hub" is not a sanctioned AppDbContext-injecting suffix), which also
/// makes the connect-resolution logic mockable without a database.
/// </summary>
public class NotificationHubTests
{
    private readonly Mock<IPushSubscriptionData> _data = new();
    private readonly Mock<IHubCallerClients> _clients = new();
    private readonly Mock<HubCallerContext> _context = new();
    private readonly Mock<IGroupManager> _groups = new();

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private NotificationHub CreateHub(Guid? userId = null)
    {
        ClaimsPrincipal? user = null;
        if (userId is not null)
        {
            user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                "test"));
        }

        _context.Setup(c => c.User).Returns(user!);
        _context.Setup(c => c.ConnectionId).Returns("conn-1");
        _context.Setup(c => c.ConnectionAborted).Returns(CT);

        var hub = new NotificationHub(_data.Object, NullLogger<NotificationHub>.Instance)
        {
            Clients = _clients.Object,
            Context = _context.Object,
            Groups = _groups.Object,
        };

        return hub;
    }

    [Fact]
    public void Constructor_ValidArgs_CreatesInstance()
    {
        var hub = CreateHub();
        hub.Should().NotBeNull();
    }

    [Fact]
    public void FamilyGroup_GivenGuid_ReturnsFormattedString()
    {
        var id = Guid.NewGuid();
        NotificationHub.FamilyGroup(id).Should().Be($"family-{id}");
    }

    [Fact]
    public async Task OnConnectedAsync_NullUser_DoesNotAddToGroup()
    {
        var hub = CreateHub();

        await hub.OnConnectedAsync();

        _groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _data.Verify(d => d.GetActiveFamilyIdForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_UserWithNoFamilyMember_DoesNotAddToGroup()
    {
        var userId = Guid.NewGuid();
        _data.Setup(d => d.GetActiveFamilyIdForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var hub = CreateHub(userId);
        await hub.OnConnectedAsync();

        _groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_UserWithActiveFamily_AddsToCorrectGroup()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        _data.Setup(d => d.GetActiveFamilyIdForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(familyId);

        var hub = CreateHub(userId);
        await hub.OnConnectedAsync();

        _groups.Verify(
            g => g.AddToGroupAsync("conn-1", $"family-{familyId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_NullException_Completes()
    {
        var hub = CreateHub();
        var act = () => hub.OnDisconnectedAsync(null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_Completes()
    {
        var hub = CreateHub();
        var act = () => hub.OnDisconnectedAsync(new InvalidOperationException("test"));
        await act.Should().NotThrowAsync();
    }
}
