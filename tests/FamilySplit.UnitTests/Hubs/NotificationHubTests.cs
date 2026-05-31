using System.Security.Claims;
using FamilySplit.Api.Hubs;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Hubs;

public class NotificationHubTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<NotificationHub>> _logger = new();
    private readonly Mock<IHubCallerClients> _clients = new();
    private readonly Mock<HubCallerContext> _context = new();
    private readonly Mock<IGroupManager> _groups = new();

    public NotificationHubTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

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

        var hub = new NotificationHub(_db, _logger.Object)
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
    }

    [Fact]
    public async Task OnConnectedAsync_UserWithNoFamilyMember_DoesNotAddToGroup()
    {
        var hub = CreateHub(Guid.NewGuid());

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
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = userId,
            IsActive = true,
            DisplayName = "Test",
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var hub = CreateHub(userId);
        await hub.OnConnectedAsync();

        _groups.Verify(
            g => g.AddToGroupAsync("conn-1", $"family-{familyId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_InactiveMember_DoesNotAddToGroup()
    {
        var userId = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            UserId = userId,
            IsActive = false,
            DisplayName = "Inactive",
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var hub = CreateHub(userId);
        await hub.OnConnectedAsync();

        _groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
