using FamilySplit.Application.Auth;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Auth;

public class RefreshTokenServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<RefreshTokenService>> _loggerMock = new();
    private readonly RefreshTokenService _sut;
    private readonly IConfiguration _config;

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshLifetimeDays"] = "7",
                ["Jwt:RefreshReuseWindowMinutes"] = "30",
            })
            .Build();

        _sut = new RefreshTokenService(_db, _config, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var sut = new RefreshTokenService(_db, _config, _loggerMock.Object);
        sut.Should().NotBeNull();
    }

    // ── TokenLifetime ──

    [Fact]
    public void TokenLifetime_ConfiguredValue_ReturnsConfiguredDays()
    {
        _sut.TokenLifetime.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void TokenLifetime_MissingConfig_DefaultsTo30Days()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var sut = new RefreshTokenService(_db, config, _loggerMock.Object);
        sut.TokenLifetime.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void TokenLifetime_InvalidConfig_DefaultsTo30Days()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshLifetimeDays"] = "abc" })
            .Build();
        var sut = new RefreshTokenService(_db, config, _loggerMock.Object);
        sut.TokenLifetime.Should().Be(TimeSpan.FromDays(30));
    }

    // ── ReuseWindow ──

    [Fact]
    public void ReuseWindow_ConfiguredValue_ReturnsConfiguredMinutes()
    {
        _sut.ReuseWindow.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void ReuseWindow_MissingConfig_DefaultsTo60Minutes()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var sut = new RefreshTokenService(_db, config, _loggerMock.Object);
        sut.ReuseWindow.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void ReuseWindow_InvalidConfig_DefaultsTo60Minutes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:RefreshReuseWindowMinutes"] = "xyz" })
            .Build();
        var sut = new RefreshTokenService(_db, config, _loggerMock.Object);
        sut.ReuseWindow.Should().Be(TimeSpan.FromMinutes(60));
    }

    // ── IssueAsync ──

    [Fact]
    public async Task IssueAsync_ValidUser_ReturnsTokenAndPersists()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();

        var result = await _sut.IssueAsync(userId, "1.2.3.4", "TestAgent", ct);

        result.Secret.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        var stored = await _db.RefreshTokens.SingleAsync(ct);
        stored.UserId.Should().Be(userId);
        stored.CreatedFromIp.Should().Be("1.2.3.4");
        stored.UserAgent.Should().Be("TestAgent");
        stored.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_NullIpAndAgent_StoresNulls()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.IssueAsync(Guid.NewGuid(), null, null, ct);

        result.Secret.Should().NotBeNullOrWhiteSpace();
        var stored = await _db.RefreshTokens.SingleAsync(ct);
        stored.CreatedFromIp.Should().BeNull();
        stored.UserAgent.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_LongIp_TrimmedTo45()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.IssueAsync(Guid.NewGuid(), new string('x', 100), null, ct);

        var stored = await _db.RefreshTokens.SingleAsync(ct);
        stored.CreatedFromIp.Should().HaveLength(45);
    }

    [Fact]
    public async Task IssueAsync_LongUserAgent_TrimmedTo512()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.IssueAsync(Guid.NewGuid(), null, new string('a', 1000), ct);

        var stored = await _db.RefreshTokens.SingleAsync(ct);
        stored.UserAgent.Should().HaveLength(512);
    }

    // ── RotateAsync ──

    [Fact]
    public async Task RotateAsync_EmptySecret_ReturnsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.RotateAsync("", null, null, ct);
        result.Should().Be(RefreshTokenService.RotateResult.RejectedInstance);
    }

    [Fact]
    public async Task RotateAsync_WhitespaceSecret_ReturnsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.RotateAsync("   ", null, null, ct);
        result.Should().Be(RefreshTokenService.RotateResult.RejectedInstance);
    }

    [Fact]
    public async Task RotateAsync_UnknownToken_ReturnsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.RotateAsync("unknown-secret", null, null, ct);
        result.Should().Be(RefreshTokenService.RotateResult.RejectedInstance);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_RotatesAndReturnsSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshLifetimeDays"] = "7",
                ["Jwt:RefreshReuseWindowMinutes"] = "0",
            })
            .Build();
        var sut = new RefreshTokenService(_db, config, _loggerMock.Object);

        var issued = await sut.IssueAsync(userId, null, null, ct);

        var token = await _db.RefreshTokens.SingleAsync(ct);
        token.CreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        await _db.SaveChangesAsync(ct);

        var result = await sut.RotateAsync(issued.Secret, "5.6.7.8", "Agent2", ct);

        result.Should().BeOfType<RefreshTokenService.RotateResult.Success>();
        var success = (RefreshTokenService.RotateResult.Success)result;
        success.UserId.Should().Be(userId);
        success.Secret.Should().NotBeNullOrWhiteSpace();

        var old = await _db.RefreshTokens.FirstAsync(t => t.Id == token.Id, ct);
        old.RevokedAt.Should().NotBeNull();
        old.ReplacedByTokenId.Should().NotBeNull();
    }

    [Fact]
    public async Task RotateAsync_ExpiredToken_ReturnsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var issued = await _sut.IssueAsync(userId, null, null, ct);

        var token = await _db.RefreshTokens.SingleAsync(ct);
        token.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync(ct);

        var result = await _sut.RotateAsync(issued.Secret, null, null, ct);
        result.Should().Be(RefreshTokenService.RotateResult.RejectedInstance);
    }

    [Fact]
    public async Task RotateAsync_RevokedWithActiveReplacement_ReturnsConcurrentRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var issued = await _sut.IssueAsync(userId, null, null, ct);

        var token = await _db.RefreshTokens.SingleAsync(ct);
        var replacementId = Guid.NewGuid();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = replacementId,
            UserId = userId,
            TokenHash = new byte[32],
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = null,
        });

        token.RevokedAt = DateTimeOffset.UtcNow;
        token.ReplacedByTokenId = replacementId;
        await _db.SaveChangesAsync(ct);

        var result = await _sut.RotateAsync(issued.Secret, null, null, ct);
        result.Should().Be(RefreshTokenService.RotateResult.ConcurrentRetryInstance);
    }

    [Fact]
    public async Task RotateAsync_WithinReuseWindow_ReturnsReused()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var issued = await _sut.IssueAsync(userId, null, null, ct);

        var result = await _sut.RotateAsync(issued.Secret, null, null, ct);

        result.Should().BeOfType<RefreshTokenService.RotateResult.Reused>();
        var reused = (RefreshTokenService.RotateResult.Reused)result;
        reused.UserId.Should().Be(userId);
    }

    // ── RevokeAsync ──

    [Fact]
    public async Task RevokeAsync_NullSecret_ReturnsEarly()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.RevokeAsync(null, ct);
        _db.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAsync_EmptySecret_ReturnsEarly()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.RevokeAsync("", ct);
        _db.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAsync_WhitespaceSecret_ReturnsEarly()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.RevokeAsync("   ", ct);
        _db.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAsync_UnknownSecret_DoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        await _sut.IssueAsync(userId, null, null, ct);

        await _sut.RevokeAsync("unknown-secret", ct);

        var token = await _db.RefreshTokens.SingleAsync(ct);
        token.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_ValidSecret_SetsRevokedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var issued = await _sut.IssueAsync(userId, null, null, ct);

        await _sut.RevokeAsync(issued.Secret, ct);

        var token = await _db.RefreshTokens.SingleAsync(ct);
        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedToken_DoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var issued = await _sut.IssueAsync(userId, null, null, ct);

        // Revoke once
        await _sut.RevokeAsync(issued.Secret, ct);
        var token = await _db.RefreshTokens.SingleAsync(ct);
        var firstRevokedAt = token.RevokedAt;

        // Second revoke with same secret should find nothing (already revoked)
        await _sut.RevokeAsync(issued.Secret, ct);
        await _db.Entry(token).ReloadAsync(ct);
        token.RevokedAt.Should().Be(firstRevokedAt);
    }

}
