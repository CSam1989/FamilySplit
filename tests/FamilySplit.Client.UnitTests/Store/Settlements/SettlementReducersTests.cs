using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Settlements;
using FamilySplit.Domain.Enums;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.Settlements;

public class SettlementReducersTests
{
    private readonly SettlementState _initial = new()
    {
        IsLoading = false,
        ErrorMessage = "old error",
    };

    [Fact]
    public void OnLoadBalances_SetsIsLoadingTrue_ClearsError()
    {
        var result = SettlementReducers.OnLoadBalances(_initial);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadBalancesSuccess_SetsBalances_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var balances = new List<FamilyBalanceDto> { new(Guid.NewGuid(), "Test", 10m, "EUR") };
        var action = new LoadBalancesSuccessAction(balances);

        var result = SettlementReducers.OnLoadBalancesSuccess(state, action);

        result.IsLoading.Should().BeFalse();
        result.Balances.Should().BeSameAs(balances);
    }

    [Fact]
    public void OnLoadBalancesFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var action = new LoadBalancesFailureAction("fail");

        var result = SettlementReducers.OnLoadBalancesFailure(state, action);

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("fail");
    }

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_ClearsError()
    {
        var result = SettlementReducers.OnLoad(_initial);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsSettlements_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var settlements = new List<SettlementSummaryDto>();
        var action = new LoadSettlementsSuccessAction(settlements);

        var result = SettlementReducers.OnLoadSuccess(state, action);

        result.IsLoading.Should().BeFalse();
        result.Settlements.Should().BeSameAs(settlements);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var action = new LoadSettlementsFailureAction("load failed");

        var result = SettlementReducers.OnLoadFailure(state, action);

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("load failed");
    }

    [Fact]
    public void OnGenerate_SetsIsGeneratingTrue_ClearsError()
    {
        var result = SettlementReducers.OnGenerate(_initial);

        result.IsGenerating.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnGenerateSuccess_SetsSettlements_ClearsGenerating()
    {
        var state = _initial with { IsGenerating = true };
        var settlements = new List<SettlementSummaryDto>();
        var action = new GenerateSettlementsSuccessAction(settlements);

        var result = SettlementReducers.OnGenerateSuccess(state, action);

        result.IsGenerating.Should().BeFalse();
        result.Settlements.Should().BeSameAs(settlements);
    }

    [Fact]
    public void OnGenerateFailure_SetsErrorMessage_ClearsGenerating()
    {
        var state = _initial with { IsGenerating = true };
        var action = new GenerateSettlementsFailureAction("gen failed");

        var result = SettlementReducers.OnGenerateFailure(state, action);

        result.IsGenerating.Should().BeFalse();
        result.ErrorMessage.Should().Be("gen failed");
    }

    [Fact]
    public void OnLoadDetail_SetsIsLoadingTrue_ClearsErrorAndSelectedSettlement()
    {
        var state = _initial with { SelectedSettlement = new SettlementDetailDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", Guid.NewGuid(), "B", 100m, "EUR", SettlementStatus.Proposed, null, [], DateTimeOffset.UtcNow, null) };

        var result = SettlementReducers.OnLoadDetail(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SelectedSettlement.Should().BeNull();
    }

    private static SettlementDetailDto CreateDetail(Guid? id = null, SettlementStatus status = SettlementStatus.Proposed) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Payer", Guid.NewGuid(), "Receiver", 50m, "EUR", status, null, [], DateTimeOffset.UtcNow, null);

    private static SettlementSummaryDto CreateSummary(Guid id, SettlementStatus status = SettlementStatus.Proposed) =>
        new(id, Guid.NewGuid(), Guid.NewGuid(), "Payer", Guid.NewGuid(), "Receiver", 50m, "EUR", status, DateTimeOffset.UtcNow, null);

    private static GroupSettlementSummaryDto CreateGroupSummary(Guid id, SettlementStatus status = SettlementStatus.Proposed) =>
        new(id, Guid.NewGuid(), Guid.NewGuid(), "Activity", Guid.NewGuid(), "Payer", Guid.NewGuid(), "Receiver", 50m, "EUR", status, DateTimeOffset.UtcNow);

    [Fact]
    public void OnLoadDetailSuccess_SetsSelectedSettlement_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var detail = CreateDetail();
        var action = new LoadSettlementDetailSuccessAction(detail);

        var result = SettlementReducers.OnLoadDetailSuccess(state, action);

        result.IsLoading.Should().BeFalse();
        result.SelectedSettlement.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnLoadDetailFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var action = new LoadSettlementDetailFailureAction("not found");

        var result = SettlementReducers.OnLoadDetailFailure(state, action);

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("not found");
    }

    [Fact]
    public void OnConfirmSent_SetsIsLoadingTrue_ClearsError()
    {
        var result = SettlementReducers.OnConfirmSent(_initial);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnConfirmSentSuccess_UpdatesSelectedAndLists()
    {
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var state = _initial with
        {
            IsLoading = true,
            Settlements = [CreateSummary(id), CreateSummary(otherId)],
            GroupSettlements = [CreateGroupSummary(id), CreateGroupSummary(otherId)],
            MyPendingSettlements = [CreateGroupSummary(id)],
        };
        var updatedDetail = CreateDetail(id, SettlementStatus.PayerSent);
        var action = new ConfirmSentSuccessAction(updatedDetail);

        var result = SettlementReducers.OnConfirmSentSuccess(state, action);

        result.IsLoading.Should().BeFalse();
        result.SelectedSettlement.Should().BeSameAs(updatedDetail);
        result.Settlements.Should().HaveCount(2);
        result.Settlements.First(s => s.Id == id).Status.Should().Be(SettlementStatus.PayerSent);
        result.Settlements.First(s => s.Id == otherId).Status.Should().Be(SettlementStatus.Proposed);
        result.GroupSettlements.First(s => s.Id == id).Status.Should().Be(SettlementStatus.PayerSent);
        result.MyPendingSettlements.First(s => s.Id == id).Status.Should().Be(SettlementStatus.PayerSent);
    }

    [Fact]
    public void OnConfirmSentSuccess_NoMatchingId_LeavesListsUnchanged()
    {
        var otherId = Guid.NewGuid();
        var state = _initial with
        {
            IsLoading = true,
            Settlements = [CreateSummary(otherId)],
            GroupSettlements = [CreateGroupSummary(otherId)],
            MyPendingSettlements = [CreateGroupSummary(otherId)],
        };
        var detail = CreateDetail(Guid.NewGuid(), SettlementStatus.PayerSent);
        var action = new ConfirmSentSuccessAction(detail);

        var result = SettlementReducers.OnConfirmSentSuccess(state, action);

        result.Settlements.First().Status.Should().Be(SettlementStatus.Proposed);
        result.GroupSettlements.First().Status.Should().Be(SettlementStatus.Proposed);
        result.MyPendingSettlements.First().Status.Should().Be(SettlementStatus.Proposed);
    }

    [Fact]
    public void OnConfirmSentFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var action = new ConfirmSentFailureAction("confirm failed");

        var result = SettlementReducers.OnConfirmSentFailure(state, action);

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("confirm failed");
    }

    [Fact]
    public void OnConfirmReceived_SetsIsLoadingTrue_ClearsError()
    {
        var result = SettlementReducers.OnConfirmReceived(_initial);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnConfirmReceivedSuccess_UpdatesSelectedAndRemovesFromGroupAndPending()
    {
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var state = _initial with
        {
            IsLoading = true,
            Settlements = [CreateSummary(id), CreateSummary(otherId)],
            GroupSettlements = [CreateGroupSummary(id), CreateGroupSummary(otherId)],
            MyPendingSettlements = [CreateGroupSummary(id), CreateGroupSummary(otherId)],
        };
        var updatedDetail = CreateDetail(id, SettlementStatus.Completed);
        var action = new ConfirmReceivedSuccessAction(updatedDetail);

        var result = SettlementReducers.OnConfirmReceivedSuccess(state, action);

        result.IsLoading.Should().BeFalse();
        result.SelectedSettlement.Should().BeSameAs(updatedDetail);
        result.Settlements.Should().HaveCount(2);
        result.Settlements.First(s => s.Id == id).Status.Should().Be(SettlementStatus.Completed);
        result.Settlements.First(s => s.Id == otherId).Status.Should().Be(SettlementStatus.Proposed);
        result.GroupSettlements.Should().HaveCount(1);
        result.GroupSettlements.Should().NotContain(s => s.Id == id);
        result.MyPendingSettlements.Should().HaveCount(1);
        result.MyPendingSettlements.Should().NotContain(s => s.Id == id);
    }

    [Fact]
    public void OnConfirmReceivedSuccess_NoMatchingId_LeavesSettlementsUnchanged()
    {
        var otherId = Guid.NewGuid();
        var state = _initial with
        {
            IsLoading = true,
            Settlements = [CreateSummary(otherId)],
            GroupSettlements = [CreateGroupSummary(otherId)],
            MyPendingSettlements = [CreateGroupSummary(otherId)],
        };
        var detail = CreateDetail(Guid.NewGuid(), SettlementStatus.Completed);
        var action = new ConfirmReceivedSuccessAction(detail);

        var result = SettlementReducers.OnConfirmReceivedSuccess(state, action);

        result.Settlements.First().Status.Should().Be(SettlementStatus.Proposed);
        result.GroupSettlements.Should().HaveCount(1);
        result.MyPendingSettlements.Should().HaveCount(1);
    }

    [Fact]
    public void OnConfirmReceivedFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoading = true };
        var action = new ConfirmReceivedFailureAction("recv failed");

        var result = SettlementReducers.OnConfirmReceivedFailure(state, action);

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("recv failed");
    }

    [Fact]
    public void OnLoadGroupSettlements_SetsIsLoadingGroupSettlementsTrue_ClearsError()
    {
        var result = SettlementReducers.OnLoadGroupSettlements(_initial);

        result.IsLoadingGroupSettlements.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadGroupSettlementsSuccess_SetsGroupSettlements_ClearsLoading()
    {
        var state = _initial with { IsLoadingGroupSettlements = true };
        var settlements = new List<GroupSettlementSummaryDto> { CreateGroupSummary(Guid.NewGuid()) };
        var action = new LoadGroupSettlementsSuccessAction(settlements);

        var result = SettlementReducers.OnLoadGroupSettlementsSuccess(state, action);

        result.IsLoadingGroupSettlements.Should().BeFalse();
        result.GroupSettlements.Should().BeSameAs(settlements);
    }

    [Fact]
    public void OnLoadGroupSettlementsFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoadingGroupSettlements = true };
        var action = new LoadGroupSettlementsFailureAction("group fail");

        var result = SettlementReducers.OnLoadGroupSettlementsFailure(state, action);

        result.IsLoadingGroupSettlements.Should().BeFalse();
        result.ErrorMessage.Should().Be("group fail");
    }

    [Fact]
    public void OnClearGroupSettlements_ClearsGroupSettlements_ClearsLoading()
    {
        var state = _initial with
        {
            IsLoadingGroupSettlements = true,
            GroupSettlements = [CreateGroupSummary(Guid.NewGuid())],
        };

        var result = SettlementReducers.OnClearGroupSettlements(state);

        result.GroupSettlements.Should().BeEmpty();
        result.IsLoadingGroupSettlements.Should().BeFalse();
    }

    [Fact]
    public void OnLoadMyPending_SetsIsLoadingMyPendingTrue_ClearsError()
    {
        var result = SettlementReducers.OnLoadMyPending(_initial);

        result.IsLoadingMyPending.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadMyPendingSuccess_SetsMyPendingSettlements_ClearsLoading()
    {
        var state = _initial with { IsLoadingMyPending = true };
        var settlements = new List<GroupSettlementSummaryDto> { CreateGroupSummary(Guid.NewGuid()) };
        var action = new LoadMyPendingSettlementsSuccessAction(settlements);

        var result = SettlementReducers.OnLoadMyPendingSuccess(state, action);

        result.IsLoadingMyPending.Should().BeFalse();
        result.MyPendingSettlements.Should().BeSameAs(settlements);
    }

    [Fact]
    public void OnLoadMyPendingFailure_SetsErrorMessage_ClearsLoading()
    {
        var state = _initial with { IsLoadingMyPending = true };
        var action = new LoadMyPendingSettlementsFailureAction("pending fail");

        var result = SettlementReducers.OnLoadMyPendingFailure(state, action);

        result.IsLoadingMyPending.Should().BeFalse();
        result.ErrorMessage.Should().Be("pending fail");
    }

    [Fact]
    public void OnClear_ResetsSettlementsBalancesSelectedAndError()
    {
        var state = _initial with
        {
            Settlements = [CreateSummary(Guid.NewGuid())],
            Balances = [new FamilyBalanceDto(Guid.NewGuid(), "Test", 10m, "EUR")],
            SelectedSettlement = CreateDetail(),
            ErrorMessage = "some error",
        };

        var result = SettlementReducers.OnClear(state);

        result.Settlements.Should().BeEmpty();
        result.Balances.Should().BeEmpty();
        result.SelectedSettlement.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage()
    {
        var state = _initial with { ErrorMessage = "an error" };

        var result = SettlementReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnClearError_WhenNoError_RemainsNull()
    {
        var state = _initial with { ErrorMessage = null };

        var result = SettlementReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
    }
}
