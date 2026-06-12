using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Delete;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Features.Expenses.Delete;

public class DeleteExpenseCommandHandlerTests : ExpenseTestBase
{
    private readonly DeleteExpenseCommandHandler _sut;

    public DeleteExpenseCommandHandlerTests()
    {
        _sut = new DeleteExpenseCommandHandler(
            Db, Audit, Guard, NullLogger<DeleteExpenseCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ExpenseNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(Guid.NewGuid(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Expense not found.*");
    }

    [Fact]
    public async Task Handle_ActivityNotFound_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = Guid.NewGuid(),
            PaidByUserId = CallerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Activity not found.*");
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_SettledActivity_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Settled);
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task Handle_LockedExpense_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            Status = ExpenseStatus.Locked,
        });
        await Db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task Handle_CallerFromDifferentFamily_ThrowsForbidden()
    {
        var (outsiderId, expenseId) = await SeedExpenseByCallerWithOutsiderAsync();

        Func<Task> act = () => _sut.HandleAsync(expenseId, outsiderId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Valid_RemovesExpense()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "ToDelete",
            TotalAmount = 99,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        await _sut.HandleAsync(expenseId, CallerId, CT);

        Db.Expenses.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Valid_CreatesAuditEntry()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Audited",
            TotalAmount = 50,
            Currency = "USD",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        await _sut.HandleAsync(expenseId, CallerId, CT);

        var auditEntry = await Db.AuditLogs.FirstOrDefaultAsync(CT);
        auditEntry.Should().NotBeNull();
        auditEntry!.EntityType.Should().Be("Expense");
        auditEntry.Action.Should().Be("Deleted");
        auditEntry.EntityId.Should().Be(expenseId);
    }
}
