using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Dashboard;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.Client.UnitTests.Store.Dashboard;

public class DashboardEffectsTests
{
    private readonly Mock<IDashboardClient> _clientMock = new();
    private readonly Mock<ILogger<DashboardEffects>> _loggerMock = new();
    private readonly Mock<IDispatcher> _dispatcherMock = new();
    private readonly DashboardEffects _sut;

    public DashboardEffectsTests()
    {
        _sut = new DashboardEffects(_clientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        // Arrange
        var stats = new List<DashboardGroupStatDto>
        {
            new(Guid.NewGuid(), "Group1", 5, 2, 2, 1, 100m, 50m, 60m, 30m, "EUR", 10m, 1, "Activity1", "Open"),
        };
        _clientMock.Setup(c => c.GetStatsAsync()).ReturnsAsync(stats);

        // Act
        await _sut.HandleLoad(_dispatcherMock.Object);

        // Assert
        _dispatcherMock.Verify(d => d.Dispatch(It.Is<LoadDashboardStatsSuccessAction>(a => a.Stats == stats)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_ClientThrows_DispatchesFailureAction()
    {
        // Arrange
        _clientMock.Setup(c => c.GetStatsAsync()).ThrowsAsync(new InvalidOperationException("Network error"));

        // Act
        await _sut.HandleLoad(_dispatcherMock.Object);

        // Assert
        _dispatcherMock.Verify(d => d.Dispatch(It.Is<LoadDashboardStatsFailureAction>(a => a.ErrorMessage.Length > 0)), Times.Once);
    }
}
