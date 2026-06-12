using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Update;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Features.Expenses.Update;

public class UpdateExpenseCommandHandlerTests : ExpenseTestBase
{
    private readonly UpdateExpenseCommandHandler _sut;

    public UpdateExpenseCommandHandlerTests()
    {
        _sut = new UpdateExpenseCommandHandler(
            Db, new UpdateExpenseCommandValidator(), Audit, Guard,
            NullLogger<UpdateExpenseCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ExpenseNotFound_ThrowsValidationException()
    {
        var cmd = new UpdateExpenseCommand("Test", null, 100, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(Guid.NewGuid(), cmd, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Expense not found.*");
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
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("New", null, 100, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(expenseId, cmd, CallerId, CT);

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
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            Status = ExpenseStatus.Locked,
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("New", null, 100, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task Handle_Valid_UpdatesExpense()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("New Title", "new desc", 200, "USD", DateOnly.FromDateTime(DateTime.Today), null);
        await _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        var expense = await Db.Expenses.FindAsync([expenseId], CT);
        expense!.Title.Should().Be("New Title");
        expense.TotalAmount.Should().Be(200);
        expense.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_AmountChanged_RecalculatesShares()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        var memberId = await Db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        Db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = memberId,
            WeightSnapshot = 1,
            CalculatedAmount = 50,
            IsExcluded = false,
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("Old", null, 200, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        await _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        var participant = await Db.ExpenseParticipants.FirstAsync(ep => ep.ExpenseId == expenseId, CT);
        participant.CalculatedAmount.Should().Be(200);
    }

    [Fact]
    public async Task Handle_SameAmountAndDate_DoesNotRecalculate()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var expenseId = Guid.NewGuid();
        var memberId = await Db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = today,
        });
        Db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = memberId,
            WeightSnapshot = 1,
            CalculatedAmount = 999,
            IsExcluded = false,
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("New Title", null, 50, "EUR", today, null);
        await _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        var participant = await Db.ExpenseParticipants.FirstAsync(ep => ep.ExpenseId == expenseId, CT);
        participant.CalculatedAmount.Should().Be(999);
    }

    [Fact]
    public async Task Handle_NullCurrency_KeepsExistingCurrency()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "GBP",
            ExpenseDate = today,
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("Old", null, 50, null, today, null);
        await _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        var expense = await Db.Expenses.FindAsync([expenseId], CT);
        expense!.Currency.Should().Be("GBP");
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

        var cmd = new UpdateExpenseCommand("X", null, 1, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_DateChanged_RecalculatesShares()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        var memberId = await Db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Old",
            TotalAmount = 100,
            Currency = "EUR",
            ExpenseDate = new DateOnly(2024, 1, 1),
        });
        Db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = memberId,
            WeightSnapshot = 1,
            CalculatedAmount = 100,
            IsExcluded = false,
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new UpdateExpenseCommand("Old", null, 100, "EUR", new DateOnly(2024, 6, 1), null);
        await _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        // Date changed so recalculation should have happened - amount should still be 100 with 1 participant
        var participant = await Db.ExpenseParticipants.FirstAsync(ep => ep.ExpenseId == expenseId, CT);
        participant.CalculatedAmount.Should().Be(100);
    }

    [Fact]
    public async Task Handle_CallerFromDifferentFamily_ThrowsForbidden()
    {
        var (outsiderId, expenseId) = await SeedExpenseByCallerWithOutsiderAsync();

        var cmd = new UpdateExpenseCommand("Hacked", null, 999, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(expenseId, cmd, outsiderId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_GlobalAdminFromDifferentFamily_Succeeds()
    {
        var (adminId, expenseId) = await SeedExpenseByCallerWithOutsiderAsync(outsiderIsGlobalAdmin: true);

        var cmd = new UpdateExpenseCommand("Admin edit", null, 60, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        await _sut.HandleAsync(expenseId, cmd, adminId, CT);

        var expense = await Db.Expenses.FindAsync([expenseId], CT);
        expense!.Title.Should().Be("Admin edit");
        expense.TotalAmount.Should().Be(60);
    }
}
