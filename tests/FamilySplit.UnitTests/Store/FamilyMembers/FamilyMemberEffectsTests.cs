using FamilySplit.Client.Services;
using FamilySplit.Client.Store.FamilyMembers;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Store.FamilyMembers;

public class FamilyMemberEffectsTests
{
    private readonly Mock<IFamilyMemberClient> _client = new();
    private readonly Mock<ILogger<FamilyMemberEffects>> _logger = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly FamilyMemberEffects _sut;

    public FamilyMemberEffectsTests()
    {
        _sut = new FamilyMemberEffects(_client.Object, _logger.Object);
    }

    [Fact]
    public void Constructor_SetsFields()
    {
        var sut = new FamilyMemberEffects(_client.Object, _logger.Object);

        sut.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleLoadProfile_Success_DispatchesSuccessAction()
    {
        var profile = new FamilyMemberDto(
            Guid.NewGuid(), "Test", null, null, null, 1.0m,
            WeightTier.Volwassene, true, true, false, DateTimeOffset.UtcNow);
        _client.Setup(c => c.GetProfileAsync()).ReturnsAsync(profile);

        await _sut.HandleLoadProfile(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadMyProfileSuccessAction>(a => a.Profile == profile)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadProfile_Exception_DispatchesFailureAction()
    {
        _client.Setup(c => c.GetProfileAsync()).ThrowsAsync(new InvalidOperationException("fail"));

        await _sut.HandleLoadProfile(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadMyProfileFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleLoadProfile_Exception_LogsError()
    {
        var ex = new InvalidOperationException("fail");
        _client.Setup(c => c.GetProfileAsync()).ThrowsAsync(ex);

        await _sut.HandleLoadProfile(_dispatcher.Object);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                ex,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
