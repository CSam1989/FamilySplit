using FamilySplit.Features.Notifications.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Notifications.Shared;

public class SignalRNotificationServiceTests
{
    private readonly Mock<IHubContext<NotificationHub>> _hubMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly SignalRNotificationService _sut;

    public SignalRNotificationServiceTests()
    {
        // VAPID push now runs on a fresh DI scope. These tests cover SignalR delivery;
        // the scope resolves no VapidPushSender, so the background push throws
        // and is swallowed by DeliverPushAsync — harmless and out of scope here.
        var scopeMock = new Mock<IServiceScope>();
        var spMock = new Mock<IServiceProvider>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        _hubMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _clientProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SignalRNotificationService(
            _hubMock.Object, _scopeFactoryMock.Object, NullLogger<SignalRNotificationService>.Instance);
    }

    [Fact]
    public void Constructor_StoresDependencies()
    {
        var service = new SignalRNotificationService(
            _hubMock.Object, _scopeFactoryMock.Object, NullLogger<SignalRNotificationService>.Instance);

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
