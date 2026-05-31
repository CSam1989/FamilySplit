using FamilySplit.Client.Services;
using FamilySplit.Domain.Enums;
using FamilySplit.Client.Store.Admin;
using FluentAssertions;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Store.Admin;

public class AdminEffectsTests
{
    private readonly Mock<IAdminClient> _client = new();
    private readonly Mock<ILogger<AdminEffects>> _logger = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly AdminEffects _sut;

    public AdminEffectsTests()
    {
        _sut = new AdminEffects(_client.Object, _logger.Object);
    }

    [Fact]
    public void Constructor_SetsFields()
    {
        var effects = new AdminEffects(_client.Object, _logger.Object);
        effects.Should().NotBeNull();
    }

    // HandleLoad

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        var families = new List<FamilyDto>();
        _client.Setup(c => c.ListFamiliesAsync()).ReturnsAsync(families);

        await _sut.HandleLoad(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadAdminFamiliesSuccessAction>(a => a.Families == families)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_Exception_DispatchesFailureAction()
    {
        _client.Setup(c => c.ListFamiliesAsync()).ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.HandleLoad(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadAdminFamiliesFailureAction>()), Times.Once);
    }

    // HandleLoadOne

    [Fact]
    public async Task HandleLoadOne_Success_DispatchesSuccessAction()
    {
        var familyId = Guid.NewGuid();
        var family = new FamilyDto(familyId, "Test", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _client.Setup(c => c.GetFamilyAsync(familyId)).ReturnsAsync(family);

        await _sut.HandleLoadOne(new LoadAdminFamilyAction(familyId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadAdminFamilySuccessAction>(a => a.Family == family)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadOne_Exception_DispatchesFailureAction()
    {
        var familyId = Guid.NewGuid();
        _client.Setup(c => c.GetFamilyAsync(familyId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoadOne(new LoadAdminFamilyAction(familyId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadAdminFamilyFailureAction>()), Times.Once);
    }

    // HandleCreate

    [Fact]
    public async Task HandleCreate_Success_DispatchesSuccessAction()
    {
        var request = new CreateFamilyRequest("New Family");
        var family = new FamilyDto(Guid.NewGuid(), "New Family", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _client.Setup(c => c.CreateFamilyAsync(request)).ReturnsAsync(family);

        await _sut.HandleCreate(new CreateAdminFamilyAction(request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CreateAdminFamilySuccessAction>(a => a.Family == family)), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Exception_DispatchesFailureAction()
    {
        var request = new CreateFamilyRequest("X");
        _client.Setup(c => c.CreateFamilyAsync(request)).ThrowsAsync(new Exception("err"));

        await _sut.HandleCreate(new CreateAdminFamilyAction(request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateAdminFamilyFailureAction>()), Times.Once);
    }

    // HandleAddMember

    [Fact]
    public async Task HandleAddMember_Success_DispatchesSuccessAndLoadActions()
    {
        var familyId = Guid.NewGuid();
        var request = new AddFamilyMemberRequest("John", null, null, null, false);
        _client.Setup(c => c.AddMemberAsync(familyId, request)).ReturnsAsync(It.IsAny<FamilyMemberDto>());

        await _sut.HandleAddMember(new AddAdminMemberAction(familyId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<AddAdminMemberSuccessAction>(a => a.FamilyId == familyId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadAdminFamilyAction>(a => a.FamilyId == familyId)), Times.Once);
    }

    [Fact]
    public async Task HandleAddMember_Exception_DispatchesFailureAction()
    {
        var familyId = Guid.NewGuid();
        var request = new AddFamilyMemberRequest("John", null, null, null, false);
        _client.Setup(c => c.AddMemberAsync(familyId, request)).ThrowsAsync(new Exception("nope"));

        await _sut.HandleAddMember(new AddAdminMemberAction(familyId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<AddAdminMemberFailureAction>()), Times.Once);
    }

    // HandleUpdateMember

    [Fact]
    public async Task HandleUpdateMember_Success_DispatchesSuccessAction()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest("Jane", null, null, null, true);
        var member = new FamilyMemberDto(memberId, "Jane", null, null, null, 1m, WeightTier.Volwassene, true, false, true, DateTimeOffset.UtcNow);
        _client.Setup(c => c.UpdateMemberAsync(familyId, memberId, request)).ReturnsAsync(member);

        await _sut.HandleUpdateMember(new UpdateAdminMemberAction(familyId, memberId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<UpdateAdminMemberSuccessAction>(a => a.Member == member)), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateMember_Exception_DispatchesFailureAction()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest("Jane", null, null, null, false);
        _client.Setup(c => c.UpdateMemberAsync(familyId, memberId, request)).ThrowsAsync(new Exception("update failed"));

        await _sut.HandleUpdateMember(new UpdateAdminMemberAction(familyId, memberId, request), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdateAdminMemberFailureAction>()), Times.Once);
    }

    // HandleRemoveMember

    [Fact]
    public async Task HandleRemoveMember_Success_DispatchesSuccessAction()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _client.Setup(c => c.RemoveMemberAsync(familyId, memberId)).Returns(Task.CompletedTask);

        await _sut.HandleRemoveMember(new RemoveAdminMemberAction(familyId, memberId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<RemoveAdminMemberSuccessAction>(a => a.FamilyId == familyId && a.MemberId == memberId)), Times.Once);
    }

    [Fact]
    public async Task HandleRemoveMember_Exception_DispatchesFailureAction()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _client.Setup(c => c.RemoveMemberAsync(familyId, memberId)).ThrowsAsync(new Exception("remove failed"));

        await _sut.HandleRemoveMember(new RemoveAdminMemberAction(familyId, memberId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<RemoveAdminMemberFailureAction>()), Times.Once);
    }
}
