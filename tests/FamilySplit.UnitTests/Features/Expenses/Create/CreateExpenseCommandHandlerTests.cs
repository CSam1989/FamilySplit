using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Create;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Features.Expenses.Create;

public class CreateExpenseCommandHandlerTests : ExpenseTestBase
{
    private readonly CreateExpenseCommandHandler _sut;

    public CreateExpenseCommandHandlerTests()
    {
        _sut = new CreateExpenseCommandHandler(
            Db, new CreateExpenseCommandValidator(), Audit, Guard,
            NullLogger<CreateExpenseCommandHandler>.Instance);
    }

    private static CreateExpenseCommand MakeCommand(decimal amount = 100m, string title = "Dinner")
        => new(title, "desc", amount, "EUR", DateOnly.FromDateTime(DateTime.Today), null);

    [Fact]
    public async Task Handle_ActivityNotFound_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();

        Func<Task> act = () => _sut.HandleAsync(Guid.NewGuid(), MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SettledActivity_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Settled);

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Valid_CreatesExpenseAndReturnsId()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var id = await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        id.Should().NotBeEmpty();
        var expense = await Db.Expenses.FindAsync([id], CT);
        expense.Should().NotBeNull();
        expense!.Title.Should().Be("Dinner");
        expense.TotalAmount.Should().Be(100);
        expense.Currency.Should().Be("EUR");
        Db.Expenses.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NullCurrency_DefaultsToEUR()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var cmd = new CreateExpenseCommand("Test", null, 50, null, DateOnly.FromDateTime(DateTime.Today), null);
        var id = await _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        var expense = await Db.Expenses.FindAsync([id], CT);
        expense!.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Handle_TitleWithWhitespace_GetsTrimmed()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var cmd = new CreateExpenseCommand("  Trimmed  ", null, 50, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        var id = await _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        var expense = await Db.Expenses.FindAsync([id], CT);
        expense!.Title.Should().Be("Trimmed");
    }

    [Fact]
    public async Task Handle_WithActivityParticipants_SeedsExpenseParticipants()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var memberId = await Db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        Db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = ActivityId, FamilyMemberId = memberId });
        await Db.SaveChangesAsync(CT);

        await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        Db.ExpenseParticipants.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var cmd = new CreateExpenseCommand("", null, 0, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_CurrencyDiffersFromExistingExpense_ThrowsValidation()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        Db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "First",
            TotalAmount = 10,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        var cmd = new CreateExpenseCommand("Second", null, 20, "USD", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*same currency*");
    }
}
