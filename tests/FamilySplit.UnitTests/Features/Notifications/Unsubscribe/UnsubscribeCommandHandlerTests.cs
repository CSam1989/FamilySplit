using FamilySplit.Features.Notifications.Data;
using FamilySplit.Features.Notifications.Unsubscribe;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Notifications.Unsubscribe;

/// <summary>
/// Business-logic test (ADR-001): mocks <see cref="IPushSubscriptionData"/>, no database. No
/// validator scenarios — matches the legacy <c>PushNotificationService.UnsubscribeAsync</c>, which
/// applies no field validation on this path (unlike Subscribe).
/// </summary>
public class UnsubscribeCommandHandlerTests
{
    private readonly Mock<IPushSubscriptionData> _data = new();
    private readonly UnsubscribeCommandHandler _sut;

    private static CancellationToken CT => TestContext.Current.CancellationToken;
    private readonly Guid _callerId = Guid.NewGuid();

    public UnsubscribeCommandHandlerTests()
    {
        _sut = new UnsubscribeCommandHandler(_data.Object, NullLogger<UnsubscribeCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ExistingSubscription_RemovesIt()
    {
        var cmd = new UnsubscribeCommand("https://push.example.com/abc123");
        _data.Setup(d => d.RemoveSubscriptionAsync(_callerId, cmd.Endpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.HandleAsync(cmd, _callerId, CT);

        _data.Verify(d => d.RemoveSubscriptionAsync(_callerId, cmd.Endpoint, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoMatch_CompletesWithoutThrowing()
    {
        var cmd = new UnsubscribeCommand("https://push.example.com/nonexistent");
        _data.Setup(d => d.RemoveSubscriptionAsync(_callerId, cmd.Endpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _sut.HandleAsync(cmd, _callerId, CT);

        await act.Should().NotThrowAsync();
    }
}
