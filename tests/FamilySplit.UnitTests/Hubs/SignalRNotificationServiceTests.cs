using FamilySplit.Api.Hubs;
using FamilySplit.Application.Push;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Hubs;

public class SignalRNotificationServiceTests
{
    private readonly Mock<IHubContext<NotificationHub>> _hubMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();
    private readonly Mock<PushNotificationService> _vapidMock;
    private readonly Mock<ILogger<SignalRNotificationService>> _loggerMock = new();
    private readonly SignalRNotificationService _sut;

    public SignalRNotificationServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost")
            .Options;
        _vapidMock = new Mock<PushNotificationService>(
            new AppDbContext(dbOptions),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<PushNotificationService>>());

        _hubMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _clientProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SignalRNotificationService(_hubMock.Object, _vapidMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_StoresDependencies()
    {
        var service = new SignalRNotificationService(
            _hubMock.Object, _vapidMock.Object, _loggerMock.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task NotifyFamilyAsync_SendsSignalRToCorrectGroup()
    {
        var familyId = Guid.NewGuid();
        var expectedGroup = $"family-{familyId}";

        await _sut.NotifyFamilyAsync(familyId, "Title", "Message", ct: TestContext.Current.CancellationToken);

        _clientsMock.Verify(c => c.Group(expectedGroup), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object?[]>(a => a.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyFamilyAsync_NullUrl_SendsNotification()
    {
        await _sut.NotifyFamilyAsync(Guid.NewGuid(), "T", "M", url: null, ct: TestContext.Current.CancellationToken);

        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object?[]>(a => a.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyFamilyAsync_WithUrl_SendsNotification()
    {
        await _sut.NotifyFamilyAsync(Guid.NewGuid(), "T", "M", url: "/expenses/123", ct: TestContext.Current.CancellationToken);

        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object?[]>(a => a.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyFamilyAsync_SignalRThrows_DoesNotThrow()
    {
        _clientProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var act = () => _sut.NotifyFamilyAsync(Guid.NewGuid(), "T", "M", ct: TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyFamilyAsync_PassesCancellationToken()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.NotifyFamilyAsync(Guid.NewGuid(), "T", "M", ct: ct);

        _clientProxyMock.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            ct), Times.Once);
    }
}
