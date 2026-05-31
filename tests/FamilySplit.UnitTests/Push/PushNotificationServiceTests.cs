using FamilySplit.Application.Push;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Push;

public class PushNotificationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<ILogger<PushNotificationService>> _loggerMock = new();
    private readonly PushNotificationService _sut;

    private CancellationToken CT => TestContext.Current.CancellationToken;

    public PushNotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new PushNotificationService(_db, _configMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void Constructor_ValidDependencies_CreatesInstance()
    {
        var sut = new PushNotificationService(_db, _configMock.Object, _loggerMock.Object);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void GetVapidPublicKey_KeyConfigured_ReturnsKey()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("test-key");

        var result = _sut.GetVapidPublicKey();

        result.Should().Be("test-key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetVapidPublicKey_KeyMissingOrWhitespace_ThrowsInvalidOperationException(string? key)
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns(key);

        var act = () => _sut.GetVapidPublicKey();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Push:Vapid:PublicKey*not configured*");
    }

    [Fact]
    public async Task SubscribeAsync_NewEndpoint_AddsSubscription()
    {
        var userId = Guid.NewGuid();

        await _sut.SubscribeAsync(userId, "https://endpoint", "p256dh", "auth", CT);

        var sub = await _db.PushSubscriptions.SingleAsync(CT);
        sub.UserId.Should().Be(userId);
        sub.Endpoint.Should().Be("https://endpoint");
        sub.P256dh.Should().Be("p256dh");
        sub.Auth.Should().Be("auth");
    }

    [Fact]
    public async Task SubscribeAsync_ExistingEndpoint_UpdatesSubscription()
    {
        var oldUserId = Guid.NewGuid();
        _db.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = oldUserId,
            Endpoint = "https://endpoint",
            P256dh = "old-p256dh",
            Auth = "old-auth",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var newUserId = Guid.NewGuid();
        await _sut.SubscribeAsync(newUserId, "https://endpoint", "new-p256dh", "new-auth", CT);

        var sub = await _db.PushSubscriptions.SingleAsync(CT);
        sub.UserId.Should().Be(newUserId);
        sub.P256dh.Should().Be("new-p256dh");
        sub.Auth.Should().Be("new-auth");
    }

    [Fact]
    public async Task UnsubscribeAsync_ExistingSubscription_RemovesIt()
    {
        var userId = Guid.NewGuid();
        _db.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = "https://endpoint",
            P256dh = "p",
            Auth = "a",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.UnsubscribeAsync(userId, "https://endpoint", CT);

        (await _db.PushSubscriptions.CountAsync(CT)).Should().Be(0);
    }

    [Fact]
    public async Task UnsubscribeAsync_NoMatch_DoesNothing()
    {
        await _sut.UnsubscribeAsync(Guid.NewGuid(), "https://nonexistent", CT);

        (await _db.PushSubscriptions.CountAsync(CT)).Should().Be(0);
    }

    [Fact]
    public async Task UnsubscribeAsync_DifferentUser_DoesNotRemove()
    {
        var userId = Guid.NewGuid();
        _db.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = "https://endpoint",
            P256dh = "p",
            Auth = "a",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.UnsubscribeAsync(Guid.NewGuid(), "https://endpoint", CT);

        (await _db.PushSubscriptions.CountAsync(CT)).Should().Be(1);
    }

    [Fact]
    public async Task SendToFamilyAsync_NoVapidKeys_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns((string?)null);
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns((string?)null);

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);
    }

    [Fact]
    public async Task SendToFamilyAsync_NoActiveMembers_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);
    }

    [Fact]
    public async Task SendToFamilyAsync_MembersButNoSubscriptions_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");

        var familyId = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = Guid.NewGuid(),
            IsActive = true,
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.SendToFamilyAsync(familyId, "title", "body", ct: CT);
    }

    [Fact]
    public async Task SendToFamilyAsync_InactiveMembersOnly_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");

        var familyId = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = Guid.NewGuid(),
            IsActive = false,
            DisplayName = "Inactive",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.SendToFamilyAsync(familyId, "title", "body", ct: CT);
    }

    [Fact]
    public async Task SendToFamilyAsync_MembersWithNullUserId_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");

        var familyId = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = null,
            IsActive = true,
            DisplayName = "Child",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.SendToFamilyAsync(familyId, "title", "body", ct: CT);
    }

    [Fact]
    public async Task SendToFamilyAsync_PublicKeyBlank_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("  ");
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);
    }

    [Fact]
    public async Task SendToFamilyAsync_PrivateKeyBlank_ReturnsEarly()
    {
        _configMock.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _configMock.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("");

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);
    }
}
