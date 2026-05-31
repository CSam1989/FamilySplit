using FamilySplit.Client.Services;
using FluentAssertions;
using Moq;

namespace FamilySplit.UnitTests.Services;

public class IWhoAmIApiTests
{
    [Fact]
    public async Task GetAsync_WhenCalled_ReturnsWhoAmIResponse()
    {
        // Arrange
        var expected = new WhoAmIResponse(
            Guid.NewGuid(),
            "test@example.com",
            "Test User",
            "https://example.com/avatar.png",
            "Google",
            DateTimeOffset.UtcNow,
            false);

        var mock = new Mock<IWhoAmIApi>();
        mock.Setup(x => x.GetAsync()).ReturnsAsync(expected);

        // Act
        var result = await mock.Object.GetAsync();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetAsync_WithNullAvatarUrl_ReturnsResponseWithNullAvatar()
    {
        // Arrange
        var expected = new WhoAmIResponse(
            Guid.NewGuid(),
            "user@test.com",
            "No Avatar",
            null,
            "Microsoft",
            DateTimeOffset.UtcNow,
            true);

        var mock = new Mock<IWhoAmIApi>();
        mock.Setup(x => x.GetAsync()).ReturnsAsync(expected);

        // Act
        var result = await mock.Object.GetAsync();

        // Assert
        result.Should().Be(expected);
        result.AvatarUrl.Should().BeNull();
        result.IsGlobalAdmin.Should().BeTrue();
    }
}
