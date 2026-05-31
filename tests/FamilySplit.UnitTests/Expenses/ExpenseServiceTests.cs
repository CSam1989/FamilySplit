using FamilySplit.Application.Audit;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Expenses;
using FamilySplit.Application.Expenses.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Expenses;

public class ExpenseServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly Mock<ILogger<ExpenseService>> _loggerMock = new();
    private readonly ExpenseService _sut;

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    // Shared test data
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _activityId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();

    public ExpenseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _audit = new AuditService(_db, new Mock<ILogger<AuditService>>().Object);
        _sut = new ExpenseService(_db, new CreateExpenseValidator(), new UpdateExpenseValidator(), _audit, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedGroupMembershipAsync()
    {
        _db.Families.Add(new Family { Id = _familyId, Name = "TestFamily" });
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            UserId = _callerId,
            DisplayName = "Caller",
            IsActive = true,
        });
        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = _groupId, FamilyId = _familyId });
        await _db.SaveChangesAsync(CT);
    }

    private async Task SeedActivityAsync(ActivityStatus status = ActivityStatus.Open)
    {
        _db.Activities.Add(new Activity
        {
            Id = _activityId,
            GroupId = _groupId,
            Name = "TestActivity",
            Status = status,
            CreatedByUserId = _callerId,
        });
        await _db.SaveChangesAsync(CT);
    }

    private static CreateExpenseRequest MakeCreateRequest(decimal amount = 100m, string title = "Dinner")
        => new(title, "desc", amount, "EUR", DateOnly.FromDateTime(DateTime.Today), null);

    // ── Constructor ──

    [Fact]
    public void Constructor_ValidArgs_CreatesInstance()
    {
        _sut.Should().NotBeNull();
    }

    // ── ListAsync ──

    [Fact]
    public async Task ListAsync_ActivityNotFound_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();

        Func<Task> act = () => _sut.ListAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Activity not found.");
    }

    [Fact]
    public async Task ListAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();

        Func<Task> act = () => _sut.ListAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListAsync_NoExpenses_ReturnsEmptyList()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var result = await _sut.ListAsync(_activityId, _callerId, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_WithExpenses_ReturnsSummaryDtos()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        _db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Lunch",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_activityId, _callerId, CT);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Lunch");
        result[0].PaidByName.Should().Be("Caller");
        result[0].PaidByFamilyName.Should().Be("TestFamily");
    }

    [Fact]
    public async Task ListAsync_PayerNotFound_ReturnsUnknown()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        _db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PaidByUserId = Guid.NewGuid(),
            Title = "Test",
            TotalAmount = 10,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_activityId, _callerId, CT);

        result[0].PaidByName.Should().Be("Unknown");
        result[0].PaidByFamilyName.Should().Be("Unknown");
        result[0].PaidByFamilyId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task ListAsync_WithParticipants_ReturnsCorrectCount()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Test",
            TotalAmount = 100,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.FamilyMembers.Add(new FamilyMember { Id = memberId, FamilyId = _familyId, DisplayName = "M1", IsActive = true });
        _db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expenseId, FamilyMemberId = memberId, IsExcluded = false });
        _db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expenseId, FamilyMemberId = memberId, IsExcluded = true });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_activityId, _callerId, CT);

        result[0].ParticipantCount.Should().Be(1);
    }

    // ── GetDetailAsync ──

    [Fact]
    public async Task GetDetailAsync_ExpenseNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.GetDetailAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Expense not found.");
    }

    [Fact]
    public async Task GetDetailAsync_ActivityNotFound_ThrowsValidationException()
    {
        _db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        var expenseId = _db.Expenses.First().Id;
        Func<Task> act = () => _sut.GetDetailAsync(expenseId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Activity not found.");
    }

    [Fact]
    public async Task GetDetailAsync_CallerNotMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.GetDetailAsync(expenseId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetDetailAsync_Valid_ReturnsDetailDto()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Dinner",
            TotalAmount = 75,
            Currency = "USD",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetDetailAsync(expenseId, _callerId, CT);

        result.Title.Should().Be("Dinner");
        result.TotalAmount.Should().Be(75);
        result.Currency.Should().Be("USD");
        result.PaidByName.Should().Be("Caller");
    }

    // ── CreateAsync ──

    [Fact]
    public async Task CreateAsync_ActivityNotFound_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();

        Func<Task> act = () => _sut.CreateAsync(Guid.NewGuid(), MakeCreateRequest(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_SettledActivity_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Settled);

        Func<Task> act = () => _sut.CreateAsync(_activityId, MakeCreateRequest(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task CreateAsync_CallerNotMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();

        Func<Task> act = () => _sut.CreateAsync(_activityId, MakeCreateRequest(), _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesExpenseAndReturnsDetail()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var result = await _sut.CreateAsync(_activityId, MakeCreateRequest(), _callerId, CT);

        result.Title.Should().Be("Dinner");
        result.TotalAmount.Should().Be(100);
        result.Currency.Should().Be("EUR");
        _db.Expenses.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_NullCurrency_DefaultsToEUR()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var req = new CreateExpenseRequest("Test", null, 50, null, DateOnly.FromDateTime(DateTime.Today), null);
        var result = await _sut.CreateAsync(_activityId, req, _callerId, CT);

        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task CreateAsync_TitleWithWhitespace_GetsTrimmed()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var req = new CreateExpenseRequest("  Trimmed  ", null, 50, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        var result = await _sut.CreateAsync(_activityId, req, _callerId, CT);

        result.Title.Should().Be("Trimmed");
    }

    [Fact]
    public async Task CreateAsync_WithActivityParticipants_SeedsExpenseParticipants()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var memberId = await _db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = _activityId, FamilyMemberId = memberId });
        await _db.SaveChangesAsync(CT);

        await _sut.CreateAsync(_activityId, MakeCreateRequest(), _callerId, CT);

        _db.ExpenseParticipants.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var req = new CreateExpenseRequest("", null, 0, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.CreateAsync(_activityId, req, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── UpdateAsync ──

    [Fact]
    public async Task UpdateAsync_ExpenseNotFound_ThrowsValidationException()
    {
        var req = new UpdateExpenseRequest("Test", null, 100, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.UpdateAsync(Guid.NewGuid(), req, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Expense not found.");
    }

    [Fact]
    public async Task UpdateAsync_SettledActivity_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Settled);

        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("New", null, 100, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.UpdateAsync(expenseId, req, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task UpdateAsync_LockedExpense_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            Status = ExpenseStatus.Locked,
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("New", null, 100, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.UpdateAsync(expenseId, req, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task UpdateAsync_Valid_UpdatesExpense()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("New Title", "new desc", 200, "USD", DateOnly.FromDateTime(DateTime.Today), null);
        var result = await _sut.UpdateAsync(expenseId, req, _callerId, CT);

        result.Title.Should().Be("New Title");
        result.TotalAmount.Should().Be(200);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateAsync_AmountChanged_RecalculatesShares()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        var memberId = await _db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        _db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = memberId,
            WeightSnapshot = 1,
            CalculatedAmount = 50,
            IsExcluded = false,
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("Old", null, 200, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        await _sut.UpdateAsync(expenseId, req, _callerId, CT);

        var participant = await _db.ExpenseParticipants.FirstAsync(ep => ep.ExpenseId == expenseId, CT);
        participant.CalculatedAmount.Should().Be(200);
    }

    [Fact]
    public async Task UpdateAsync_SameAmountAndDate_DoesNotRecalculate()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var expenseId = Guid.NewGuid();
        var memberId = await _db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = today,
        });
        _db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = memberId,
            WeightSnapshot = 1,
            CalculatedAmount = 999,
            IsExcluded = false,
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("New Title", null, 50, "EUR", today, null);
        await _sut.UpdateAsync(expenseId, req, _callerId, CT);

        var participant = await _db.ExpenseParticipants.FirstAsync(ep => ep.ExpenseId == expenseId, CT);
        participant.CalculatedAmount.Should().Be(999);
    }

    [Fact]
    public async Task UpdateAsync_NullCurrency_KeepsExistingCurrency()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 50,
            Currency = "GBP",
            ExpenseDate = today,
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("Old", null, 50, null, today, null);
        var result = await _sut.UpdateAsync(expenseId, req, _callerId, CT);

        result.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task UpdateAsync_CallerNotMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("X", null, 1, "EUR", DateOnly.FromDateTime(DateTime.Today), null);
        Func<Task> act = () => _sut.UpdateAsync(expenseId, req, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_DateChanged_RecalculatesShares()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var expenseId = Guid.NewGuid();
        var memberId = await _db.FamilyMembers.Select(fm => fm.Id).FirstAsync(CT);
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Old",
            TotalAmount = 100,
            Currency = "EUR",
            ExpenseDate = new DateOnly(2024, 1, 1),
        });
        _db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = memberId,
            WeightSnapshot = 1,
            CalculatedAmount = 100,
            IsExcluded = false,
        });
        await _db.SaveChangesAsync(CT);

        var req = new UpdateExpenseRequest("Old", null, 100, "EUR", new DateOnly(2024, 6, 1), null);
        await _sut.UpdateAsync(expenseId, req, _callerId, CT);

        // Date changed so recalculation should have happened - amount should still be 100 with 1 participant
        var participant = await _db.ExpenseParticipants.FirstAsync(ep => ep.ExpenseId == expenseId, CT);
        participant.CalculatedAmount.Should().Be(100);
    }

    // ── DeleteAsync ──

    [Fact]
    public async Task DeleteAsync_ExpenseNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.DeleteAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Expense not found.");
    }

    [Fact]
    public async Task DeleteAsync_ActivityNotFound_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = Guid.NewGuid(),
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.DeleteAsync(expenseId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Activity not found.");
    }

    [Fact]
    public async Task DeleteAsync_CallerNotMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.DeleteAsync(expenseId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteAsync_SettledActivity_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Settled);
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.DeleteAsync(expenseId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task DeleteAsync_LockedExpense_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "X",
            TotalAmount = 1,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            Status = ExpenseStatus.Locked,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.DeleteAsync(expenseId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task DeleteAsync_Valid_RemovesExpense()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "ToDelete",
            TotalAmount = 99,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        await _sut.DeleteAsync(expenseId, _callerId, CT);

        _db.Expenses.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Valid_CreatesAuditEntry()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Audited",
            TotalAmount = 50,
            Currency = "USD",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await _db.SaveChangesAsync(CT);

        await _sut.DeleteAsync(expenseId, _callerId, CT);

        var auditEntry = await _db.AuditLogs.FirstOrDefaultAsync(CT);
        auditEntry.Should().NotBeNull();
        auditEntry!.EntityType.Should().Be("Expense");
        auditEntry.Action.Should().Be("Deleted");
        auditEntry.EntityId.Should().Be(expenseId);
    }
}
