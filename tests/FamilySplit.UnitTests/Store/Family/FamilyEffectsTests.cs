using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Family;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FamilySplit.UnitTests.Store.Family;

public class FamilyEffectsTests
{
    private readonly Mock<IFamilyClient> _clientMock = new();
    private readonly Mock<ILogger<FamilyEffects>> _loggerMock = new();
    private readonly Mock<IDispatcher> _dispatcherMock = new();
    private readonly FamilyEffects _sut;

    private static FamilyDto CreateFamilyDto() =>
        new(Guid.NewGuid(), "Test", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static FamilyMemberDto CreateMemberDto() =>
        new(Guid.NewGuid(), "Member", null, null, null, 1m, WeightTier.Volwassene, true, false, false, DateTimeOffset.UtcNow);

    public FamilyEffectsTests()
    {
        _sut = new FamilyEffects(_clientMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        var family = CreateFamilyDto();
        _clientMock.Setup(c => c.GetMyFamilyAsync()).ReturnsAsync(family);

        await _sut.HandleLoad(_dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.Is<LoadMyFamilySuccessAction>(a => a.Family == family)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_Exception_DispatchesFailureAction()
    {
        _clientMock.Setup(c => c.GetMyFamilyAsync()).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoad(_dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<LoadMyFamilyFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleRename_Success_DispatchesSuccessAction()
    {
        var request = new UpdateFamilyNameRequest("New Name");
        var family = CreateFamilyDto();
        _clientMock.Setup(c => c.UpdateFamilyNameAsync(request)).ReturnsAsync(family);

        await _sut.HandleRename(new UpdateFamilyNameAction(request), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.Is<UpdateFamilyNameSuccessAction>(a => a.Family == family)), Times.Once);
    }

    [Fact]
    public async Task HandleRename_Exception_DispatchesFailureAction()
    {
        var request = new UpdateFamilyNameRequest("New Name");
        _clientMock.Setup(c => c.UpdateFamilyNameAsync(request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleRename(new UpdateFamilyNameAction(request), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<UpdateFamilyNameFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleAddMember_Success_DispatchesSuccessAndLoadActions()
    {
        var request = new AddFamilyMemberRequest("John", null, null, null, false);
        var member = CreateMemberDto();
        _clientMock.Setup(c => c.AddMemberAsync(request)).ReturnsAsync(member);

        await _sut.HandleAddMember(new AddFamilyMemberAction(request), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.Is<AddFamilyMemberSuccessAction>(a => a.Member == member)), Times.Once);
        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<LoadMyFamilyAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleAddMember_Exception_DispatchesFailureAction()
    {
        var request = new AddFamilyMemberRequest("John", null, null, null, false);
        _clientMock.Setup(c => c.AddMemberAsync(request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleAddMember(new AddFamilyMemberAction(request), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<AddFamilyMemberFailureAction>()), Times.Once);
        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<LoadMyFamilyAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpdateMember_Success_DispatchesSuccessAction()
    {
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest("Jane", null, null, null, false);
        var member = CreateMemberDto();
        _clientMock.Setup(c => c.UpdateMemberAsync(memberId, request)).ReturnsAsync(member);

        await _sut.HandleUpdateMember(new UpdateFamilyMemberAction(memberId, request), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.Is<UpdateFamilyMemberSuccessAction>(a => a.Member == member)), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateMember_Exception_DispatchesFailureAction()
    {
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest("Jane", null, null, null, false);
        _clientMock.Setup(c => c.UpdateMemberAsync(memberId, request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleUpdateMember(new UpdateFamilyMemberAction(memberId, request), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<UpdateFamilyMemberFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleRemoveMember_Success_DispatchesSuccessAction()
    {
        var memberId = Guid.NewGuid();

        await _sut.HandleRemoveMember(new RemoveFamilyMemberAction(memberId), _dispatcherMock.Object);

        _clientMock.Verify(c => c.RemoveMemberAsync(memberId), Times.Once);
        _dispatcherMock.Verify(d => d.Dispatch(It.Is<RemoveFamilyMemberSuccessAction>(a => a.MemberId == memberId)), Times.Once);
    }

    [Fact]
    public async Task HandleRemoveMember_Exception_DispatchesFailureAction()
    {
        var memberId = Guid.NewGuid();
        _clientMock.Setup(c => c.RemoveMemberAsync(memberId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleRemoveMember(new RemoveFamilyMemberAction(memberId), _dispatcherMock.Object);

        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<RemoveFamilyMemberFailureAction>()), Times.Once);
        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<RemoveFamilyMemberSuccessAction>()), Times.Never);
    }
}
