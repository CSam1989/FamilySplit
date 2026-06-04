using System.Net;
using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Auth;
using FluentAssertions;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Xunit;

namespace FamilySplit.Client.UnitTests.Store.Auth;

public class AuthEffectsTests
{
    private readonly Mock<IAuthApi> _authApi = new();
    private readonly Mock<IWhoAmIApi> _whoAmIApi = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly AuthService _authService;
    private readonly TestNavigationManager _nav = new();
    private readonly AuthEffects _sut;

    public AuthEffectsTests()
    {
        _authService = new AuthService(_authApi.Object, Mock.Of<ILogger<AuthService>>());
        _sut = new AuthEffects(_authService, _whoAmIApi.Object, _nav);
    }

    [Fact]
    public async Task HandleCheckAuth_BothAttemptsReturnFalse_DispatchesNotAuthenticated()
    {
        // Auth will fail (no refresh token setup) => IsAuthenticatedAsync returns false
        _authApi.Setup(x => x.RefreshAsync()).ThrowsAsync(new HttpRequestException());

        await _sut.HandleCheckAuth(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CheckAuthNotAuthenticatedAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleCheckAuth_FirstAttemptSucceeds_DispatchesSuccess()
    {
        var response = new RefreshResponse("valid-token", 3600);
        _authApi.Setup(x => x.RefreshAsync()).ReturnsAsync(response);
        var user = new WhoAmIResponse(Guid.NewGuid(), "test@test.com", "Test", null, "Google", DateTimeOffset.UtcNow, false);
        _whoAmIApi.Setup(x => x.GetAsync()).ReturnsAsync(user);

        await _sut.HandleCheckAuth(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CheckAuthSuccessAction>(a => a.User == user)), Times.Once);
    }

    [Fact]
    public async Task HandleCheckAuth_AuthenticatedButWhoAmIFails_LogsOutAndDispatchesNotAuthenticated()
    {
        _authApi.Setup(x => x.RefreshAsync()).ReturnsAsync(new RefreshResponse("token", 3600));
        _whoAmIApi.Setup(x => x.GetAsync()).ThrowsAsync(new HttpRequestException());

        await _sut.HandleCheckAuth(_dispatcher.Object);

        _authApi.Verify(a => a.LogoutAsync(), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CheckAuthNotAuthenticatedAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleCheckAuth_FirstFailsSecondSucceeds_DispatchesSuccess()
    {
        var callCount = 0;
        _authApi.Setup(x => x.RefreshAsync()).ReturnsAsync(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException();
            }

            return new RefreshResponse("token", 3600);
        });
        var user = new WhoAmIResponse(Guid.NewGuid(), "a@b.com", "A", null, "Google", DateTimeOffset.UtcNow, false);
        _whoAmIApi.Setup(x => x.GetAsync()).ReturnsAsync(user);

        await _sut.HandleCheckAuth(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CheckAuthSuccessAction>(a => a.User == user)), Times.Once);
    }

    [Fact]
    public async Task HandleSignOut_CallsLogoutAndNavigatesToRoot()
    {
        await _sut.HandleSignOut(_dispatcher.Object);

        _authApi.Verify(a => a.LogoutAsync(), Times.Once);
        _nav.LastUri.Should().Be("/");
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public string? LastUri { get; private set; }

        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastUri = uri;
        }
    }
}
