using FamilySplit.Features.Notifications.Data;
using FamilySplit.Features.Notifications.Subscribe;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Notifications.Subscribe;

/// <summary>
/// Business-logic test (ADR-001): mocks <see cref="IPushSubscriptionData"/>, no database.
/// </summary>
public class SubscribeCommandHandlerTests
{
    private readonly Mock<IPushSubscriptionData> _data = new();
    private readonly SubscribeCommandHandler _sut;

    private static CancellationToken CT => TestContext.Current.CancellationToken;
    private readonly Guid _callerId = Guid.NewGuid();

    public SubscribeCommandHandlerTests()
    {
        _sut = new SubscribeCommandHandler(
            _data.Object, new SubscribeCommandValidator(), NullLogger<SubscribeCommandHandler>.Instance);
    }

    private static SubscribeCommand MakeCommand(
        string endpoint = "https://push.example.com/abc123",
        string p256dh = "p256dh-key",
        string auth = "auth-secret")
        => new(endpoint, p256dh, auth);

    [Fact]
    public async Task Handle_ValidCommand_PersistsSubscription()
    {
        var cmd = MakeCommand();

        await _sut.HandleAsync(cmd, _callerId, CT);

        _data.Verify(d => d.UpsertSubscriptionAsync(
            _callerId, cmd.Endpoint, cmd.P256dh, cmd.Auth, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidEndpoint_ThrowsValidationException_AndDoesNotPersist()
    {
        var cmd = MakeCommand(endpoint: "not-a-url");

        Func<Task> act = () => _sut.HandleAsync(cmd, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        _data.Verify(d => d.UpsertSubscriptionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyP256dh_ThrowsValidationException_AndDoesNotPersist()
    {
        var cmd = MakeCommand(p256dh: "");

        Func<Task> act = () => _sut.HandleAsync(cmd, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        _data.Verify(d => d.UpsertSubscriptionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
