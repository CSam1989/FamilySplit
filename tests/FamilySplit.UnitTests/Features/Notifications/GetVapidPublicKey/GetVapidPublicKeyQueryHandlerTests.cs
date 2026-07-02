using FamilySplit.Features.Notifications.GetVapidPublicKey;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Notifications.GetVapidPublicKey;

/// <summary>
/// Classified as a Query for consistency, but it reads only <see cref="IConfiguration"/> — no
/// database, so this is a plain unit test (not Testcontainers).
/// </summary>
public class GetVapidPublicKeyQueryHandlerTests
{
    private readonly Mock<IConfiguration> _config = new();
    private readonly GetVapidPublicKeyQueryHandler _sut;

    public GetVapidPublicKeyQueryHandlerTests()
    {
        _sut = new GetVapidPublicKeyQueryHandler(_config.Object, NullLogger<GetVapidPublicKeyQueryHandler>.Instance);
    }

    [Fact]
    public void Handle_KeyConfigured_ReturnsKey()
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns("test-key");

        var result = _sut.Handle();

        result.Should().Be("test-key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Handle_KeyMissingOrWhitespace_ThrowsInvalidOperationException(string? key)
    {
        _config.Setup(c => c["Push:Vapid:PublicKey"]).Returns(key);

        var act = () => _sut.Handle();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Push:Vapid:PublicKey*not configured*");
    }
}
