using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Expenses.List;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Features.Expenses.List;

public class ListExpensesQueryHandlerTests : ExpenseTestBase
{
    private readonly ListExpensesQueryHandler _sut;

    public ListExpensesQueryHandlerTests()
    {
        _sut = new ListExpensesQueryHandler(Db, Guard, NullLogger<ListExpensesQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ActivityNotFound_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();

        Func<Task> act = () => _sut.HandleAsync(Guid.NewGuid(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Activity not found.*");
    }

    [Fact]
    public async Task Handle_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NoExpenses_ReturnsEmptyList()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var result = await _sut.HandleAsync(ActivityId, CallerId, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithExpenses_ReturnsSummaryDtos()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        Db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Lunch",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync(CT);

        var result = await _sut.HandleAsync(ActivityId, CallerId, CT);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Lunch");
        result[0].PaidByName.Should().Be("Caller");
        result[0].PaidByFamilyName.Should().Be("TestFamily");
    }

    [Fact]
    public async Task Handle_PayerNotFound_ReturnsUnknown()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        Db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = ActivityId,
            PaidByUserId = Guid.NewGuid(),
            Title = "Test",
            TotalAmount = 10,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync(CT);

        var result = await _sut.HandleAsync(ActivityId, CallerId, CT);

        result[0].PaidByName.Should().Be("Unknown");
        result[0].PaidByFamilyName.Should().Be("Unknown");
        result[0].PaidByFamilyId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Handle_WithParticipants_ReturnsCorrectCount()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId,
            Title = "Test",
            TotalAmount = 100,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        Db.FamilyMembers.Add(new FamilyMember { Id = memberId, FamilyId = FamilyId, DisplayName = "M1", IsActive = true });
        Db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expenseId, FamilyMemberId = memberId, IsExcluded = false });
        Db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expenseId, FamilyMemberId = memberId, IsExcluded = true });
        await Db.SaveChangesAsync(CT);

        var result = await _sut.HandleAsync(ActivityId, CallerId, CT);

        result[0].ParticipantCount.Should().Be(1);
    }
}
