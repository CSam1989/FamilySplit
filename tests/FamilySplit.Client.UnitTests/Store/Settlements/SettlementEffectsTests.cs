using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Activities;
using FamilySplit.Client.Store.Settlements;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.Client.UnitTests.Store.Settlements;

public class SettlementEffectsTests
{
    private readonly Mock<ISettlementClient> _client = new();
    private readonly Mock<ILogger<SettlementEffects>> _logger = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly SettlementEffects _sut;

    public SettlementEffectsTests()
    {
        _sut = new SettlementEffects(_client.Object, _logger.Object);
    }

    // --- HandleLoadBalances ---

    [Fact]
    public async Task HandleLoadBalances_Success_DispatchesSuccessAction()
    {
        var action = new LoadBalancesAction(Guid.NewGuid(), Guid.NewGuid());
        var balances = new List<FamilyBalanceDto>();
        _client.Setup(c => c.GetBalancesAsync(action.GroupId, action.ActivityId)).ReturnsAsync(balances);

        await _sut.HandleLoadBalances(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesSuccessAction>(a => a.Balances == balances)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadBalances_Exception_DispatchesFailureAction()
    {
        var action = new LoadBalancesAction(Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.GetBalancesAsync(action.GroupId, action.ActivityId)).ThrowsAsync(new InvalidOperationException("fail"));

        await _sut.HandleLoadBalances(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadBalancesFailureAction>()), Times.Once);
    }

    // --- HandleLoad ---

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        var action = new LoadSettlementsAction(Guid.NewGuid(), Guid.NewGuid());
        var settlements = new List<SettlementSummaryDto>();
        _client.Setup(c => c.ListAsync(action.GroupId, action.ActivityId)).ReturnsAsync(settlements);

        await _sut.HandleLoad(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadSettlementsSuccessAction>(a => a.Settlements == settlements)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_Exception_DispatchesFailureAction()
    {
        var action = new LoadSettlementsAction(Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.ListAsync(action.GroupId, action.ActivityId)).ThrowsAsync(new Exception("err"));

        await _sut.HandleLoad(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadSettlementsFailureAction>()), Times.Once);
    }

    // --- HandleGenerate ---

    [Fact]
    public async Task HandleGenerate_Success_DispatchesSuccessAndLoadActivityDetail()
    {
        var action = new GenerateSettlementsAction(Guid.NewGuid(), Guid.NewGuid());
        var settlements = new List<SettlementSummaryDto>();
        _client.Setup(c => c.GenerateAsync(action.GroupId, action.ActivityId)).ReturnsAsync(settlements);

        await _sut.HandleGenerate(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<GenerateSettlementsSuccessAction>(a => a.Settlements == settlements)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
    }

    [Fact]
    public async Task HandleGenerate_Exception_DispatchesFailureAction()
    {
        var action = new GenerateSettlementsAction(Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.GenerateAsync(action.GroupId, action.ActivityId)).ThrowsAsync(new Exception("err"));

        await _sut.HandleGenerate(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<GenerateSettlementsFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadActivityDetailAction>()), Times.Never);
    }

    // --- HandleLoadDetail ---

    [Fact]
    public async Task HandleLoadDetail_Success_DispatchesSuccessAction()
    {
        var action = new LoadSettlementDetailAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var detail = new SettlementDetailDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Payer", Guid.NewGuid(), "Receiver", 100m, "EUR", SettlementStatus.Proposed, null, [], DateTimeOffset.UtcNow, null);
        _client.Setup(c => c.GetDetailAsync(action.GroupId, action.ActivityId, action.SettlementId)).ReturnsAsync(detail);

        await _sut.HandleLoadDetail(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadSettlementDetailSuccessAction>(a => a.Settlement == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Exception_DispatchesFailureAction()
    {
        var action = new LoadSettlementDetailAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.GetDetailAsync(action.GroupId, action.ActivityId, action.SettlementId)).ThrowsAsync(new Exception("err"));

        await _sut.HandleLoadDetail(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadSettlementDetailFailureAction>()), Times.Once);
    }

    // --- HandleConfirmSent ---

    [Fact]
    public async Task HandleConfirmSent_Success_DispatchesSuccessAction()
    {
        var action = new ConfirmSentAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var detail = new SettlementDetailDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Payer", Guid.NewGuid(), "Receiver", 50m, "EUR", SettlementStatus.Proposed, null, [], DateTimeOffset.UtcNow, null);
        _client.Setup(c => c.ConfirmSentAsync(action.GroupId, action.ActivityId, action.SettlementId)).ReturnsAsync(detail);

        await _sut.HandleConfirmSent(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<ConfirmSentSuccessAction>(a => a.Settlement == detail)), Times.Once);
    }

    [Fact]
    public async Task HandleConfirmSent_Exception_DispatchesFailureAction()
    {
        var action = new ConfirmSentAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.ConfirmSentAsync(action.GroupId, action.ActivityId, action.SettlementId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleConfirmSent(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<ConfirmSentFailureAction>()), Times.Once);
    }

    // --- HandleConfirmReceived ---

    [Fact]
    public async Task HandleConfirmReceived_Success_DispatchesSuccessAndReloadActions()
    {
        var action = new ConfirmReceivedAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var detail = new SettlementDetailDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Payer", Guid.NewGuid(), "Receiver", 50m, "EUR", SettlementStatus.Proposed, null, [], DateTimeOffset.UtcNow, null);
        _client.Setup(c => c.ConfirmReceivedAsync(action.GroupId, action.ActivityId, action.SettlementId)).ReturnsAsync(detail);

        await _sut.HandleConfirmReceived(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<ConfirmReceivedSuccessAction>(a => a.Settlement == detail)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadSettlementsAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadActivityDetailAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadGroupSettlementsAction>(a => a.GroupId == action.GroupId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadMyPendingSettlementsAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleConfirmReceived_Exception_DispatchesFailureAction()
    {
        var action = new ConfirmReceivedAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.ConfirmReceivedAsync(action.GroupId, action.ActivityId, action.SettlementId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleConfirmReceived(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<ConfirmReceivedFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadSettlementsAction>()), Times.Never);
    }

    // --- HandleLoadGroupSettlements ---

    [Fact]
    public async Task HandleLoadGroupSettlements_Success_DispatchesSuccessAction()
    {
        var action = new LoadGroupSettlementsAction(Guid.NewGuid());
        var settlements = new List<GroupSettlementSummaryDto>();
        _client.Setup(c => c.ListForGroupAsync(action.GroupId)).ReturnsAsync(settlements);

        await _sut.HandleLoadGroupSettlements(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadGroupSettlementsSuccessAction>(a => a.Settlements == settlements)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadGroupSettlements_Exception_DispatchesFailureAction()
    {
        var action = new LoadGroupSettlementsAction(Guid.NewGuid());
        _client.Setup(c => c.ListForGroupAsync(action.GroupId)).ThrowsAsync(new Exception("err"));

        await _sut.HandleLoadGroupSettlements(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadGroupSettlementsFailureAction>()), Times.Once);
    }

    // --- HandleLoadMyPending ---

    [Fact]
    public async Task HandleLoadMyPending_Success_DispatchesSuccessAction()
    {
        var settlements = new List<GroupSettlementSummaryDto>();
        _client.Setup(c => c.ListMyPendingAsync()).ReturnsAsync(settlements);

        await _sut.HandleLoadMyPending(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadMyPendingSettlementsSuccessAction>(a => a.Settlements == settlements)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadMyPending_Exception_DispatchesFailureAction()
    {
        _client.Setup(c => c.ListMyPendingAsync()).ThrowsAsync(new Exception("err"));

        await _sut.HandleLoadMyPending(_dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadMyPendingSettlementsFailureAction>()), Times.Once);
    }
}
