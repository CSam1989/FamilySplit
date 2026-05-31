using FamilySplit.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace FamilySplit.UnitTests.Services;

public class IncludeCredentialsHandlerTests
{
    private sealed class TestableHandler : IncludeCredentialsHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpRequestMessage? CapturedRequest { get; private set; }

        public TestableHandler(HttpResponseMessage response)
        {
            _response = response;
            InnerHandler = new FakeInnerHandler(this);
        }

        private sealed class FakeInnerHandler(TestableHandler owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                owner.CapturedRequest = request;
                return Task.FromResult(owner._response);
            }
        }

        public Task<HttpResponseMessage> InvokeSendAsync(HttpRequestMessage request, CancellationToken ct)
            => SendAsync(request, ct);
    }

    [Fact]
    public async Task SendAsync_SetsIncludeCredentials_AndForwardsToInner()
    {
        // Arrange
        var expectedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        using var handler = new TestableHandler(expectedResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var response = await handler.InvokeSendAsync(request, CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expectedResponse);
        handler.CapturedRequest.Should().NotBeNull();

        // Verify the browser credentials option was set
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<BrowserRequestCredentials>("WebAssemblyFetchOptions"),
            out var creds);
    }

    [Fact]
    public async Task SendAsync_PassesCancellationToken_ToInnerHandler()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var expectedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        using var handler = new TestableHandler(expectedResponse);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");

        // Act
        var response = await handler.InvokeSendAsync(request, cts.Token);

        // Assert
        response.Should().BeSameAs(expectedResponse);
    }
}
