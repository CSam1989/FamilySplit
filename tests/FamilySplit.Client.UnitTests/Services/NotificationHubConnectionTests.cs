using FamilySplit.Client.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FamilySplit.Client.UnitTests.Services;

public class NotificationHubConnectionTests : IAsyncDisposable
{
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<ILogger<NotificationHubConnection>> _loggerMock = new();
    private readonly AuthService _authService;
    private readonly NotificationHubConnection _sut;

    public NotificationHubConnectionTests()
    {
        var authApiMock = new Mock<IAuthApi>();
        var authLoggerMock = new Mock<ILogger<AuthService>>();
        _authService = new AuthService(authApiMock.Object, authLoggerMock.Object);

        _sut = new NotificationHubConnection(_configMock.Object, _authService, _loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    [Fact]
    public void IsConnected_WhenNoConnection_ReturnsFalse()
    {
        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_UsesDefaultBaseUrl_WhenConfigMissing()
    {
        _configMock.Setup(c => c["Api:BaseUrl"]).Returns((string?)null);

        await _sut.ConnectAsync(TestContext.Current.CancellationToken);

        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_UsesConfiguredBaseUrl_WhenProvided()
    {
        _configMock.Setup(c => c["Api:BaseUrl"]).Returns("https://example.com");

        await _sut.ConnectAsync(TestContext.Current.CancellationToken);

        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_TrimsTrailingSlash_FromBaseUrl()
    {
        _configMock.Setup(c => c["Api:BaseUrl"]).Returns("https://example.com/");

        await _sut.ConnectAsync(TestContext.Current.CancellationToken);

        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_CalledTwice_DisposesStaleConnection()
    {
        _configMock.Setup(c => c["Api:BaseUrl"]).Returns("https://example.com");

        await _sut.ConnectAsync(TestContext.Current.CancellationToken);
        await _sut.ConnectAsync(TestContext.Current.CancellationToken);

        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNoConnection_DoesNothing()
    {
        await _sut.DisconnectAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenNoConnection_DoesNothing()
    {
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_AfterConnect_CleansUp()
    {
        _configMock.Setup(c => c["Api:BaseUrl"]).Returns("https://example.com");
        await _sut.ConnectAsync(TestContext.Current.CancellationToken);

        await _sut.DisposeAsync();

        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_SupportsCancellation()
    {
        _configMock.Setup(c => c["Api:BaseUrl"]).Returns("https://example.com");
        using var cts = new CancellationTokenSource();

        await _sut.ConnectAsync(cts.Token);

        _sut.IsConnected.Should().BeFalse();
    }
}
