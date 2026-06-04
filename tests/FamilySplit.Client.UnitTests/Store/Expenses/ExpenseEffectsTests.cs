using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Expenses;
using FamilySplit.Client.Store.Settlements;
using FamilySplit.Domain.Enums;
using Fluxor;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.Client.UnitTests.Store.Expenses;

public class ExpenseEffectsTests
{
    private readonly Mock<IExpenseClient> _client = new();
    private readonly Mock<ILogger<ExpenseEffects>> _logger = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly ExpenseEffects _sut;

    public ExpenseEffectsTests()
    {
        _sut = new ExpenseEffects(_client.Object, _logger.Object);
    }

    private static ExpenseDetailDto CreateDetail() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "T", null, 10m, "EUR", DateOnly.MinValue, "P", Guid.NewGuid(), "F", ExpenseStatus.Active, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static CreateExpenseRequest CreateRequest() =>
        new("T", null, 10m, "EUR", DateOnly.MinValue, null);

    private static UpdateExpenseRequest UpdateRequest() =>
        new("T", null, 10m, "EUR", DateOnly.MinValue, null);

    [Fact]
    public async Task HandleLoad_Success_DispatchesSuccessAction()
    {
        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var expenses = new List<ExpenseSummaryDto>();
        _client.Setup(c => c.ListAsync(groupId, activityId)).ReturnsAsync(expenses);

        await _sut.HandleLoad(new LoadExpensesAction(groupId, activityId), _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadExpensesSuccessAction>(a => a.Expenses == expenses)), Times.Once);
    }

    [Fact]
    public async Task HandleLoad_Exception_DispatchesFailureAction()
    {
        var action = new LoadExpensesAction(Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.ListAsync(action.GroupId, action.ActivityId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoad(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadExpensesFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Success_DispatchesSuccessAction()
    {
        var action = new LoadExpenseDetailAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var expense = CreateDetail();
        _client.Setup(c => c.GetAsync(action.GroupId, action.ActivityId, action.ExpenseId)).ReturnsAsync(expense);

        await _sut.HandleLoadDetail(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadExpenseDetailSuccessAction>(a => a.Expense == expense)), Times.Once);
    }

    [Fact]
    public async Task HandleLoadDetail_Exception_DispatchesFailureAction()
    {
        var action = new LoadExpenseDetailAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.GetAsync(action.GroupId, action.ActivityId, action.ExpenseId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleLoadDetail(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadExpenseDetailFailureAction>()), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Success_DispatchesSuccessAndRefreshActions()
    {
        var request = CreateRequest();
        var action = new CreateExpenseAction(Guid.NewGuid(), Guid.NewGuid(), request);
        var expense = CreateDetail();
        _client.Setup(c => c.CreateAsync(action.GroupId, action.ActivityId, request)).ReturnsAsync(expense);

        await _sut.HandleCreate(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<CreateExpenseSuccessAction>(a => a.Expense == expense)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadExpensesAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
    }

    [Fact]
    public async Task HandleCreate_Exception_DispatchesFailureAction()
    {
        var request = CreateRequest();
        var action = new CreateExpenseAction(Guid.NewGuid(), Guid.NewGuid(), request);
        _client.Setup(c => c.CreateAsync(action.GroupId, action.ActivityId, action.Request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleCreate(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<CreateExpenseFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadExpensesAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpdate_Success_DispatchesSuccessAndRefreshActions()
    {
        var request = UpdateRequest();
        var action = new UpdateExpenseAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request);
        var expense = CreateDetail();
        _client.Setup(c => c.UpdateAsync(action.GroupId, action.ActivityId, action.ExpenseId, request)).ReturnsAsync(expense);

        await _sut.HandleUpdate(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<UpdateExpenseSuccessAction>(a => a.Expense == expense)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadExpensesAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
    }

    [Fact]
    public async Task HandleUpdate_Exception_DispatchesFailureAction()
    {
        var request = UpdateRequest();
        var action = new UpdateExpenseAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request);
        _client.Setup(c => c.UpdateAsync(action.GroupId, action.ActivityId, action.ExpenseId, action.Request)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleUpdate(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<UpdateExpenseFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadExpensesAction>()), Times.Never);
    }

    [Fact]
    public async Task HandleDelete_Success_DispatchesSuccessAndRefreshActions()
    {
        var action = new DeleteExpenseAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.DeleteAsync(action.GroupId, action.ActivityId, action.ExpenseId)).Returns(Task.CompletedTask);

        await _sut.HandleDelete(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.Is<DeleteExpenseSuccessAction>(a => a.ExpenseId == action.ExpenseId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadExpensesAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.Is<LoadBalancesAction>(a => a.GroupId == action.GroupId && a.ActivityId == action.ActivityId)), Times.Once);
    }

    [Fact]
    public async Task HandleDelete_Exception_DispatchesFailureAction()
    {
        var action = new DeleteExpenseAction(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.Setup(c => c.DeleteAsync(action.GroupId, action.ActivityId, action.ExpenseId)).ThrowsAsync(new Exception("fail"));

        await _sut.HandleDelete(action, _dispatcher.Object);

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<DeleteExpenseFailureAction>()), Times.Once);
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<LoadExpensesAction>()), Times.Never);
    }
}
