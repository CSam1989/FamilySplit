using FamilySplit.Features.Notifications.Data;
using FamilySplit.Features.Notifications.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Notifications.Shared;

/// <summary>
/// Ports the legacy <c>PushNotificationServiceTests.SendToFamilyAsync_*</c> early-return coverage.
/// <see cref="VapidPushSender"/> now depends on <see cref="IPushSubscriptionData"/> (mockable) +
/// <see cref="IConfiguration"/> instead of <c>AppDbContext</c> directly (ADR-001) — no database.
/// Actual VAPID delivery (the HTTP POST to the push service) is not exercised here; only the
/// early-return / config-gate branches are, matching the legacy test's scope.
/// </summary>
public class VapidPushSenderTests
{
    private readonly Mock<IPushSubscriptionData> _data = new();
    private readonly Mock<IConfiguration> _config = new();
    private readonly VapidPushSender _sut;

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    public VapidPushSenderTests()
    {
        _sut = new VapidPushSender(_data.Object, _config.Object, NullLogger<VapidPushSender>.Instance);
    }

    [Fact]
    public async Task SendToFamilyAsync_NoVapidKeys_ReturnsEarly_DoesNotReadSubscriptions()
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns((string?)null);
        _config.Setup(c => c["Push:Vapid:PrivateKey"]).Returns((string?)null);

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);

        _data.Verify(d => d.GetActiveUserIdsForFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendToFamilyAsync_PublicKeyBlank_ReturnsEarly()
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns("  ");
        _config.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);

        _data.Verify(d => d.GetActiveUserIdsForFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendToFamilyAsync_PrivateKeyBlank_ReturnsEarly()
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _config.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("");

        await _sut.SendToFamilyAsync(Guid.NewGuid(), "title", "body", ct: CT);

        _data.Verify(d => d.GetActiveUserIdsForFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendToFamilyAsync_NoActiveMembers_ReturnsEarly_DoesNotReadSubscriptions()
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _config.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");
        var familyId = Guid.NewGuid();
        _data.Setup(d => d.GetActiveUserIdsForFamilyAsync(familyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.SendToFamilyAsync(familyId, "title", "body", ct: CT);

        _data.Verify(d => d.GetSubscriptionsForUsersAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendToFamilyAsync_MembersButNoSubscriptions_ReturnsEarly_DoesNotRemoveStale()
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns("pub");
        _config.Setup(c => c["Push:Vapid:PrivateKey"]).Returns("priv");
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _data.Setup(d => d.GetActiveUserIdsForFamilyAsync(familyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([userId]);
        _data.Setup(d => d.GetSubscriptionsForUsersAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(userId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.SendToFamilyAsync(familyId, "title", "body", ct: CT);

        _data.Verify(d => d.RemoveStaleSubscriptionsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
