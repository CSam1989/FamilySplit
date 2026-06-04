using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Expenses;
using FamilySplit.Domain.Enums;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.Expenses;

public class ExpenseReducersTests
{
    private readonly ExpenseState _initialState = new()
    {
        IsLoading = false,
        ErrorMessage = "old error",
        Expenses = [],
        SelectedExpense = null,
    };

    [Fact]
    public void OnLoad_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ExpenseReducers.OnLoad(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnLoadSuccess_SetsExpenses_AndStopsLoading()
    {
        var expenses = new List<ExpenseSummaryDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Dinner", null, 50m, "EUR", DateOnly.FromDateTime(DateTime.Today), "Alice", Guid.NewGuid(), "Family A", ExpenseStatus.Active, 3, DateTimeOffset.UtcNow),
        };
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnLoadSuccess(state, new LoadExpensesSuccessAction(expenses));

        result.IsLoading.Should().BeFalse();
        result.Expenses.Should().BeSameAs(expenses);
    }

    [Fact]
    public void OnLoadFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnLoadFailure(state, new LoadExpensesFailureAction("Something failed"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something failed");
    }

    [Fact]
    public void OnLoadDetail_SetsIsLoadingTrue_AndClearsErrorAndSelectedExpense()
    {
        var state = _initialState with { ErrorMessage = "err", SelectedExpense = CreateDetailDto() };

        var result = ExpenseReducers.OnLoadDetail(state);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SelectedExpense.Should().BeNull();
    }

    [Fact]
    public void OnLoadDetailSuccess_SetsSelectedExpense_AndStopsLoading()
    {
        var detail = CreateDetailDto();
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnLoadDetailSuccess(state, new LoadExpenseDetailSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedExpense.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnLoadDetailFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnLoadDetailFailure(state, new LoadExpenseDetailFailureAction("detail error"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("detail error");
    }

    [Fact]
    public void OnCreate_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ExpenseReducers.OnCreate(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnCreateSuccess_SetsSelectedExpense_AndStopsLoading()
    {
        var detail = CreateDetailDto();
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnCreateSuccess(state, new CreateExpenseSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedExpense.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnCreateFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnCreateFailure(state, new CreateExpenseFailureAction("create error"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("create error");
    }

    [Fact]
    public void OnUpdate_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ExpenseReducers.OnUpdate(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnUpdateSuccess_SetsSelectedExpense_AndStopsLoading()
    {
        var detail = CreateDetailDto();
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnUpdateSuccess(state, new UpdateExpenseSuccessAction(detail));

        result.IsLoading.Should().BeFalse();
        result.SelectedExpense.Should().BeSameAs(detail);
    }

    [Fact]
    public void OnUpdateFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnUpdateFailure(state, new UpdateExpenseFailureAction("update error"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("update error");
    }

    [Fact]
    public void OnDelete_SetsIsLoadingTrue_AndClearsError()
    {
        var result = ExpenseReducers.OnDelete(_initialState);

        result.IsLoading.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnDeleteSuccess_RemovesExpense_AndStopsLoading()
    {
        var expenseId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var expenses = new List<ExpenseSummaryDto>
        {
            new(expenseId, Guid.NewGuid(), "Dinner", null, 50m, "EUR", DateOnly.FromDateTime(DateTime.Today), "Alice", Guid.NewGuid(), "Family A", ExpenseStatus.Active, 3, DateTimeOffset.UtcNow),
            new(otherId, Guid.NewGuid(), "Lunch", null, 30m, "EUR", DateOnly.FromDateTime(DateTime.Today), "Bob", Guid.NewGuid(), "Family B", ExpenseStatus.Active, 2, DateTimeOffset.UtcNow),
        };
        var state = _initialState with { IsLoading = true, Expenses = expenses };

        var result = ExpenseReducers.OnDeleteSuccess(state, new DeleteExpenseSuccessAction(expenseId));

        result.IsLoading.Should().BeFalse();
        result.Expenses.Should().HaveCount(1);
        result.Expenses[0].Id.Should().Be(otherId);
    }

    [Fact]
    public void OnDeleteSuccess_ClearsSelectedExpense_WhenMatchesDeletedId()
    {
        var expenseId = Guid.NewGuid();
        var selected = CreateDetailDto(expenseId);
        var state = _initialState with { IsLoading = true, SelectedExpense = selected, Expenses = [] };

        var result = ExpenseReducers.OnDeleteSuccess(state, new DeleteExpenseSuccessAction(expenseId));

        result.SelectedExpense.Should().BeNull();
    }

    [Fact]
    public void OnDeleteSuccess_KeepsSelectedExpense_WhenDifferentId()
    {
        var selected = CreateDetailDto();
        var state = _initialState with { IsLoading = true, SelectedExpense = selected, Expenses = [] };

        var result = ExpenseReducers.OnDeleteSuccess(state, new DeleteExpenseSuccessAction(Guid.NewGuid()));

        result.SelectedExpense.Should().BeSameAs(selected);
    }

    [Fact]
    public void OnDeleteFailure_SetsErrorMessage_AndStopsLoading()
    {
        var state = _initialState with { IsLoading = true };

        var result = ExpenseReducers.OnDeleteFailure(state, new DeleteExpenseFailureAction("delete error"));

        result.IsLoading.Should().BeFalse();
        result.ErrorMessage.Should().Be("delete error");
    }

    [Fact]
    public void OnClear_ResetsExpensesAndSelectedExpenseAndError()
    {
        var state = _initialState with
        {
            Expenses = [new(Guid.NewGuid(), Guid.NewGuid(), "Dinner", null, 50m, "EUR", DateOnly.FromDateTime(DateTime.Today), "Alice", Guid.NewGuid(), "Family A", ExpenseStatus.Active, 3, DateTimeOffset.UtcNow)],
            SelectedExpense = CreateDetailDto(),
            ErrorMessage = "some error",
        };

        var result = ExpenseReducers.OnClear(state);

        result.Expenses.Should().BeEmpty();
        result.SelectedExpense.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OnClearError_ClearsErrorMessage_AndPreservesOtherState()
    {
        var selected = CreateDetailDto();
        var expenses = new List<ExpenseSummaryDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Lunch", null, 30m, "EUR", DateOnly.FromDateTime(DateTime.Today), "Bob", Guid.NewGuid(), "Family B", ExpenseStatus.Active, 2, DateTimeOffset.UtcNow),
        };
        var state = _initialState with { ErrorMessage = "err", SelectedExpense = selected, Expenses = expenses };

        var result = ExpenseReducers.OnClearError(state);

        result.ErrorMessage.Should().BeNull();
        result.SelectedExpense.Should().BeSameAs(selected);
        result.Expenses.Should().BeSameAs(expenses);
    }

    private static ExpenseDetailDto CreateDetailDto(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), "Dinner", null, 50m, "EUR", DateOnly.FromDateTime(DateTime.Today), "Alice", Guid.NewGuid(), "Family A", ExpenseStatus.Active, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
