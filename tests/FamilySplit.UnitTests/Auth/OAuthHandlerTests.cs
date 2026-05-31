using System.Net;
using System.Text.Json;
using FamilySplit.Api.Auth;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Auth;

public class OAuthHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IConfigurationSection> _sectionMock;
    private readonly Mock<ILogger<OAuthHandler>> _loggerMock;
    private readonly List<HttpClient> _clients = [];

    public OAuthHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _sectionMock = new Mock<IConfigurationSection>();
        _sectionMock.Setup(s => s["ClientId"]).Returns("test-client-id");
        _sectionMock.Setup(s => s["ClientSecret"]).Returns("test-client-secret");
        _sectionMock.Setup(s => s["TokenUrl"]).Returns("https://token.test/token");
        _sectionMock.Setup(s => s["UserInfoUrl"]).Returns("https://userinfo.test/userinfo");

        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c.GetSection("OAuth:Google")).Returns(_sectionMock.Object);

        _loggerMock = new Mock<ILogger<OAuthHandler>>();
    }

    public void Dispose()
    {
        foreach (var c in _clients)
        {
            c.Dispose();
        }

        _db.Dispose();
    }

    private OAuthHandler CreateSut(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.local") };
        _clients.Add(client);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("google-oauth")).Returns(client);
        return new OAuthHandler(_db, _configMock.Object, factoryMock.Object, _loggerMock.Object);
    }

    private OAuthHandler CreateSut() => new(_db, _configMock.Object, Mock.Of<IHttpClientFactory>(), _loggerMock.Object);

    private static FakeHandler MakeHandler(
        HttpStatusCode tokenStatus, object? tokenBody,
        HttpStatusCode userInfoStatus = HttpStatusCode.OK, object? userInfoBody = null)
        => new(tokenStatus, tokenBody, userInfoStatus, userInfoBody);

    private static FakeHandler MakeSuccessHandler(string sub, string email, string? name = "Test User", string? picture = "https://pic.url")
        => MakeHandler(
            HttpStatusCode.OK,
            new { access_token = "at", expires_in = 3600, token_type = "Bearer" },
            HttpStatusCode.OK,
            new { sub, email, name, picture });

    [Fact]
    public void Constructor_AssignsAllDependencies()
    {
        var sut = CreateSut();
        sut.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_MissingClientId_ThrowsInvalidOperation()
    {
        _sectionMock.Setup(s => s["ClientId"]).Returns((string?)null);
        var sut = CreateSut();

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ClientId*");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_MissingClientSecret_ThrowsInvalidOperation()
    {
        _sectionMock.Setup(s => s["ClientSecret"]).Returns((string?)null);
        var sut = CreateSut();

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ClientSecret*");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_TokenExchangeFails_ThrowsInvalidOperation()
    {
        var handler = MakeHandler(HttpStatusCode.BadRequest, new { error = "bad" });
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*token exchange failed*");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_EmptyTokenResponse_ThrowsInvalidOperation()
    {
        var handler = MakeHandler(HttpStatusCode.OK, null);
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*empty token response*");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_UserInfoFails_ThrowsHttpRequestException()
    {
        var handler = MakeHandler(
            HttpStatusCode.OK, new { access_token = "at", expires_in = 3600, token_type = "Bearer" },
            HttpStatusCode.InternalServerError);
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_UserInfoMissingSub_ThrowsInvalidOperation()
    {
        var handler = MakeSuccessHandler(sub: "", email: "test@example.com");
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*missing sub or email*");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_UserInfoMissingEmail_ThrowsInvalidOperation()
    {
        var handler = MakeSuccessHandler(sub: "sub123", email: "");
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*missing sub or email*");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_NoFamilyMember_ThrowsNotRegisteredException()
    {
        var handler = MakeSuccessHandler(sub: "sub123", email: "Test@Example.com");
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotRegisteredException>();
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_NewUser_CreatesUserAndLinksMember()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = MakeSuccessHandler(sub: "sub-new", email: "Test@Example.com", name: "New User", picture: "https://avatar.url");
        var sut = CreateSut(handler);

        var result = await sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Provider.Should().Be(Provider.Google);
        result.ExternalId.Should().Be("sub-new");
        result.Email.Should().Be("test@example.com");
        result.DisplayName.Should().Be("New User");
        result.AvatarUrl.Should().Be("https://avatar.url");

        var updatedMember = await _db.FamilyMembers.FirstAsync(m => m.Id == member.Id, TestContext.Current.CancellationToken);
        updatedMember.UserId.Should().Be(result.Id);
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_ExistingUser_UpdatesProfile()
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = userId,
            Provider = Provider.Google,
            ExternalId = "sub-exist",
            Email = "old@example.com",
            DisplayName = "Old",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            Email = "new@example.com",
            UserId = userId,
            DisplayName = "Member",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = MakeSuccessHandler(sub: "sub-exist", email: "New@Example.com", name: "Updated", picture: "https://new.pic");
        var sut = CreateSut(handler);

        var result = await sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        result.Id.Should().Be(userId);
        result.Email.Should().Be("new@example.com");
        result.DisplayName.Should().Be("Updated");
        result.AvatarUrl.Should().Be("https://new.pic");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_MemberLinkedToDifferentUser_ThrowsNotRegistered()
    {
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            Email = "test@example.com",
            UserId = Guid.NewGuid(), // linked to someone else
            DisplayName = "Member",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = MakeSuccessHandler(sub: "sub-imposter", email: "Test@Example.com");
        var sut = CreateSut(handler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotRegisteredException>();
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_NameIsNull_FallsBackToEmail()
    {
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = MakeSuccessHandler(sub: "sub-noname", email: "Test@Example.com", name: null, picture: null);
        var sut = CreateSut(handler);

        var result = await sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        result.DisplayName.Should().Be("Test@Example.com");
        result.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_EmailWithWhitespace_IsNormalizedToLowerTrimmed()
    {
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            Email = "spaced@example.com",
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = MakeSuccessHandler(sub: "sub-space", email: "  Spaced@Example.com  ");
        var sut = CreateSut(handler);

        var result = await sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        result.Email.Should().Be("spaced@example.com");
    }

    [Fact]
    public async Task HandleGoogleCallbackAsync_DefaultUrls_UsedWhenConfigIsNull()
    {
        _sectionMock.Setup(s => s["TokenUrl"]).Returns((string?)null);
        _sectionMock.Setup(s => s["UserInfoUrl"]).Returns((string?)null);

        var captureHandler = new UrlCapturingHandler();
        var sut = CreateSut(captureHandler);

        Func<Task> act = () => sut.HandleGoogleCallbackAsync("code", "verifier", "https://redirect", TestContext.Current.CancellationToken);

        // Will throw because handler returns 400, but we verify the default URL was used
        await act.Should().ThrowAsync<InvalidOperationException>();
        captureHandler.RequestedUrls.Should().Contain("https://oauth2.googleapis.com/token");
    }

    private sealed class FakeHandler(
        HttpStatusCode tokenStatus,
        object? tokenBody,
        HttpStatusCode userInfoStatus,
        object? userInfoBody) : HttpMessageHandler
    {
        private int _callCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _callCount++;
            if (_callCount == 1)
            {
                var content = tokenBody is null
                    ? new StringContent("null")
                    : new StringContent(JsonSerializer.Serialize(tokenBody), System.Text.Encoding.UTF8, "application/json");
                return Task.FromResult(new HttpResponseMessage(tokenStatus) { Content = content });
            }

            var uiContent = userInfoBody is null
                ? new StringContent("null")
                : new StringContent(JsonSerializer.Serialize(userInfoBody), System.Text.Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(userInfoStatus) { Content = uiContent });
        }
    }

    private sealed class UrlCapturingHandler : HttpMessageHandler
    {
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestedUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"test\"}"),
            });
        }
    }
}
