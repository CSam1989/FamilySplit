using System.Net;
using FamilySplit.Client.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;

namespace FamilySplit.UnitTests.Services;

public class JwtAuthHandlerTests
{
    private readonly Mock<IAuthApi> _authApi = new();
    private readonly Mock<ILogger<AuthService>> _logger = new();
    private readonly FakeInnerHandler _innerHandler = new();

    private AuthService CreateAuthService() => new(_authApi.Object, _logger.Object);

    private HttpMessageInvoker CreateInvoker(AuthService authService)
    {
        var handler = new JwtAuthHandler(authService)
        {
            InnerHandler = _innerHandler,
        };
        return new HttpMessageInvoker(handler);
    }

    [Fact]
    public void Constructor_StoresAuthService()
    {
        var auth = CreateAuthService();
        var handler = new JwtAuthHandler(auth);
        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task SendAsync_NonUnauthorizedResponse_ReturnsResponseDirectly()
    {
        var auth = CreateAuthService();
        _innerHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        using var invoker = CreateInvoker(auth);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _innerHandler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_RefreshSucceeds_RetriesRequest()
    {
        _authApi.Setup(a => a.RefreshAsync())
            .ReturnsAsync(new RefreshResponse("new-token", 300));

        _innerHandler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        _innerHandler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var auth = CreateAuthService();
        using var invoker = CreateInvoker(auth);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _innerHandler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task SendAsync_Unauthorized_RefreshFails_Returns401()
    {
        _authApi.Setup(a => a.RefreshAsync())
            .ThrowsAsync(await ApiException.Create(
                new HttpRequestMessage(), HttpMethod.Post,
                new HttpResponseMessage(HttpStatusCode.Unauthorized), new RefitSettings()));

        _innerHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var auth = CreateAuthService();
        using var invoker = CreateInvoker(auth);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.RequestMessage.Should().BeSameAs(request);
        _innerHandler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_AttachesToken_WhenAvailable()
    {
        _authApi.Setup(a => a.RefreshAsync())
            .ReturnsAsync(new RefreshResponse("my-jwt", 300));

        var auth = CreateAuthService();
        // Prime the token
        await auth.TryRefreshAsync();

        _innerHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        using var invoker = CreateInvoker(auth);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        await invoker.SendAsync(request, CancellationToken.None);

        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("my-jwt");
    }

    [Fact]
    public async Task SendAsync_NoToken_NoAuthorizationHeader()
    {
        // RefreshAsync returns empty token
        _authApi.Setup(a => a.RefreshAsync())
            .ReturnsAsync(new RefreshResponse("", 0));

        var auth = CreateAuthService();
        _innerHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        using var invoker = CreateInvoker(auth);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        await invoker.SendAsync(request, CancellationToken.None);

        request.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_Unauthorized_RefreshSucceeds_AttachesNewTokenOnRetry()
    {
        var callCount = 0;
        _authApi.Setup(a => a.RefreshAsync())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new RefreshResponse($"token-{callCount}", 300);
            });

        _innerHandler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        _innerHandler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var auth = CreateAuthService();
        using var invoker = CreateInvoker(auth);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
        await invoker.SendAsync(request, CancellationToken.None);

        // After retry, the request should have the refreshed token
        request.Headers.Authorization!.Parameter.Should().StartWith("token-");
    }

    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        public HttpResponseMessage? ResponseToReturn { get; set; }
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = Responses.Count > 0 ? Responses.Dequeue() : ResponseToReturn!;
            return Task.FromResult(response);
        }
    }
}
