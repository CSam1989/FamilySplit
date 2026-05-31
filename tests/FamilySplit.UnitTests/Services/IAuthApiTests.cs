using FamilySplit.Client.Services;
using FluentAssertions;
using Moq;

namespace FamilySplit.UnitTests.Services;

public class IAuthApiTests
{
    [Fact]
    public async Task RefreshAsync_ReturnsRefreshResponse()
    {
        // Arrange
        var expected = new RefreshResponse("jwt-token", 3600);
        var mock = new Mock<IAuthApi>();
        mock.Setup(x => x.RefreshAsync()).ReturnsAsync(expected);

        // Act
        var result = await mock.Object.RefreshAsync();

        // Assert
        result.Should().Be(expected);
        result.Token.Should().Be("jwt-token");
        result.ExpiresInSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task LogoutAsync_CompletesSuccessfully()
    {
        // Arrange
        var mock = new Mock<IAuthApi>();
        mock.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

        // Act
        var act = () => mock.Object.LogoutAsync();

        // Assert
        await act.Should().NotThrowAsync();
        mock.Verify(x => x.LogoutAsync(), Times.Once);
    }
}
