using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Expenses.GetDetail;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Features.Expenses.GetDetail;

public class GetExpenseDetailQueryHandlerTests : ExpenseTestBase
{
    private readonly GetExpenseDetailQueryHandler _sut;

    public GetExpenseDetailQueryHandlerTests()
    {
        _sut = new GetExpenseDetailQueryHandler(Db, Guard, NullLogger<GetExpenseDetailQueryHandler>.Instance);
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
        Db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            PaidByUserId = CallerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        var expenseId = Db.Expenses.First().Id;
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
    public async Task Handle_Valid_ReturnsDetailDto()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Dinner",
            TotalAmount = 75,
            Currency = "USD",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync(CT);

        var result = await _sut.HandleAsync(expenseId, CallerId, CT);

        result.Title.Should().Be("Dinner");
        result.TotalAmount.Should().Be(75);
        result.Currency.Should().Be("USD");
        result.PaidByName.Should().Be("Caller");
    }
}
