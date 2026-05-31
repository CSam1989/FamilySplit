using FamilySplit.Application.Audit;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Notifications;
using FamilySplit.Application.Settlements;
using FamilySplit.Application.Settlements.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Settlements;

public class SettlementServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly Mock<INotificationService> _notificationsMock = new();
    private readonly Mock<ILogger<SettlementService>> _loggerMock = new();
    private readonly SettlementService _sut;

    private static CancellationToken CT => TestContext.Current.CancellationToken;

    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _activityId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _family2Id = Guid.NewGuid();

    public SettlementServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _audit = new AuditService(_db, new Mock<ILogger<AuditService>>().Object);
        _sut = new SettlementService(_db, _audit, _notificationsMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedGroupMembershipAsync()
    {
        _db.Families.Add(new Family { Id = _familyId, Name = "Family1" });
        _db.Families.Add(new Family { Id = _family2Id, Name = "Family2" });
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            UserId = _callerId,
            DisplayName = "Caller",
            IsActive = true,
        });
        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = _groupId, FamilyId = _familyId });
        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = _groupId, FamilyId = _family2Id });
        await _db.SaveChangesAsync(CT);
    }

    private async Task SeedActivityAsync(ActivityStatus status = ActivityStatus.Open, Guid? parentActivityId = null)
    {
        _db.Activities.Add(new Activity
        {
            Id = _activityId,
            GroupId = _groupId,
            Name = "TestActivity",
            Status = status,
            CreatedByUserId = _callerId,
            ParentActivityId = parentActivityId,
        });
        await _db.SaveChangesAsync(CT);
    }

    private async Task SeedExpenseWithParticipantsAsync(decimal amount = 100m)
    {
        var paidByMemberId = _db.FamilyMembers.Local.First(m => m.FamilyId == _familyId).Id;
        var expenseId = Guid.NewGuid();
        _db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = _activityId,
            PaidByUserId = _callerId,
            Title = "Dinner",
            TotalAmount = amount,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });

        // Family2 member to participate
        var member2Id = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = member2Id,
            FamilyId = _family2Id,
            UserId = Guid.NewGuid(),
            DisplayName = "Member2",
            IsActive = true,
        });

        _db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = paidByMemberId,
            WeightSnapshot = 1,
            CalculatedAmount = amount / 2,
            IsExcluded = false,
        });
        _db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = member2Id,
            WeightSnapshot = 1,
            CalculatedAmount = amount / 2,
            IsExcluded = false,
        });

        await _db.SaveChangesAsync(CT);
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_ValidArgs_CreatesInstance()
    {
        _sut.Should().NotBeNull();
    }

    // ── GetBalancesAsync ──

    [Fact]
    public async Task GetBalancesAsync_ActivityNotFound_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();

        Func<Task> act = () => _sut.GetBalancesAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Activity not found.");
    }

    [Fact]
    public async Task GetBalancesAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();

        Func<Task> act = () => _sut.GetBalancesAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetBalancesAsync_WithExpenses_ReturnsBalances()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();
        await SeedExpenseWithParticipantsAsync();

        var result = await _sut.GetBalancesAsync(_activityId, _callerId, CT);

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(b => b.Currency.Should().Be("EUR"));
        result.Should().BeInDescendingOrder(b => b.Balance);
    }

    [Fact]
    public async Task GetBalancesAsync_NoExpenses_ReturnsEmptyList()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var result = await _sut.GetBalancesAsync(_activityId, _callerId, CT);

        result.Should().BeEmpty();
    }

    // ── GenerateAsync ──

    [Fact]
    public async Task GenerateAsync_ActivityNotFound_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();

        Func<Task> act = () => _sut.GenerateAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Activity not found.");
    }

    [Fact]
    public async Task GenerateAsync_ActivityOpen_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Open);

        Func<Task> act = () => _sut.GenerateAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*must be closed*");
    }

    [Fact]
    public async Task GenerateAsync_ActivitySettled_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Settled);

        Func<Task> act = () => _sut.GenerateAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*already settled*");
    }

    [Fact]
    public async Task GenerateAsync_ActivityAbsorbedByParent_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.AbsorbedByParent);

        Func<Task> act = () => _sut.GenerateAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*absorbed*");
    }

    [Fact]
    public async Task GenerateAsync_SubActivity_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity
        {
            Id = parentId,
            GroupId = _groupId,
            Name = "Parent",
            Status = ActivityStatus.Open,
            CreatedByUserId = _callerId,
        });
        await _db.SaveChangesAsync(CT);
        await SeedActivityAsync(ActivityStatus.Closed, parentId);

        Func<Task> act = () => _sut.GenerateAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Sub-activities*");
    }

    [Fact]
    public async Task GenerateAsync_ExistingSettlements_ReturnsExisting()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);

        var now = DateTimeOffset.UtcNow;
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = now,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GenerateAsync(_activityId, _callerId, CT);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateAsync_ZeroBalances_MarksActivitySettled()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        // No expenses → zero balances

        var result = await _sut.GenerateAsync(_activityId, _callerId, CT);

        result.Should().BeEmpty();
        var activity = await _db.Activities.FindAsync([_activityId], CT);
        activity!.Status.Should().Be(ActivityStatus.Settled);
    }

    [Fact]
    public async Task GenerateAsync_WithExpenses_CreatesSettlements()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        await SeedExpenseWithParticipantsAsync();

        var result = await _sut.GenerateAsync(_activityId, _callerId, CT);

        result.Should().NotBeEmpty();
        var settlements = await _db.Settlements.Where(s => s.ActivityId == _activityId).ToListAsync(CT);
        settlements.Should().NotBeEmpty();
        settlements.Should().AllSatisfy(s =>
        {
            s.Status.Should().Be(SettlementStatus.Proposed);
            s.Currency.Should().Be("EUR");
        });
    }

    [Fact]
    public async Task GenerateAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync(ActivityStatus.Closed);

        Func<Task> act = () => _sut.GenerateAsync(_activityId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── ListForGroupAsync ──

    [Fact]
    public async Task ListForGroupAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        Func<Task> act = () => _sut.ListForGroupAsync(_groupId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListForGroupAsync_NoActivities_ReturnsEmpty()
    {
        await SeedGroupMembershipAsync();

        var result = await _sut.ListForGroupAsync(_groupId, _callerId, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListForGroupAsync_WithSettlements_ReturnsPendingOnly()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);

        var now = DateTimeOffset.UtcNow;
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = now,
        });
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 30,
            Currency = "EUR",
            Status = SettlementStatus.Completed,
            ProposedAt = now,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListForGroupAsync(_groupId, _callerId, CT);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(SettlementStatus.Proposed);
    }

    [Fact]
    public async Task ListForGroupAsync_ExcludesSubActivities()
    {
        await SeedGroupMembershipAsync();
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity
        {
            Id = parentId,
            GroupId = _groupId,
            Name = "Parent",
            Status = ActivityStatus.Closed,
            CreatedByUserId = _callerId,
        });
        await _db.SaveChangesAsync(CT);
        await SeedActivityAsync(ActivityStatus.Closed, parentId);

        // Settlement on sub-activity
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListForGroupAsync(_groupId, _callerId, CT);

        // Sub-activity settlements should not appear (parent has no settlements)
        // The sub-activity has parentActivityId set, so it's excluded from activities query
        result.Where(r => r.ActivityId == _activityId).Should().BeEmpty();
    }

    // ── ListMyPendingAsync ──

    [Fact]
    public async Task ListMyPendingAsync_CallerNotFamilyMember_ThrowsForbiddenException()
    {
        Func<Task> act = () => _sut.ListMyPendingAsync(Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListMyPendingAsync_NoGroups_ReturnsEmpty()
    {
        // Caller has family but no groups
        _db.Families.Add(new Family { Id = _familyId, Name = "Family1" });
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            UserId = _callerId,
            DisplayName = "Caller",
            IsActive = true,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListMyPendingAsync(_callerId, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMyPendingAsync_NoActivities_ReturnsEmpty()
    {
        await SeedGroupMembershipAsync();

        var result = await _sut.ListMyPendingAsync(_callerId, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMyPendingAsync_WithSettlements_ReturnsOnlyCallerFamilySettlements()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);

        var thirdFamilyId = Guid.NewGuid();
        _db.Families.Add(new Family { Id = thirdFamilyId, Name = "Family3" });
        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = _groupId, FamilyId = thirdFamilyId });

        var now = DateTimeOffset.UtcNow;

        // Settlement involving caller's family
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = now,
        });

        // Settlement NOT involving caller's family
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = thirdFamilyId,
            Amount = 30,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = now,
        });

        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListMyPendingAsync(_callerId, CT);

        result.Should().HaveCount(1);
        var settlement = result[0];
        (settlement.PayerFamilyId == _familyId || settlement.ReceiverFamilyId == _familyId).Should().BeTrue();
    }

    [Fact]
    public async Task ListMyPendingAsync_ExcludesCompletedAndCancelled()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);

        var now = DateTimeOffset.UtcNow;
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Completed,
            ProposedAt = now,
        });
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 30,
            Currency = "EUR",
            Status = SettlementStatus.Cancelled,
            ProposedAt = now,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListMyPendingAsync(_callerId, CT);

        result.Should().BeEmpty();
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
    public async Task ListAsync_WithSettlements_ReturnsSummaryList()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);

        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_activityId, _callerId, CT);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAsync_NoSettlements_ReturnsEmptyList()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var result = await _sut.ListAsync(_activityId, _callerId, CT);

        result.Should().BeEmpty();
    }

    // ── GetDetailAsync ──

    [Fact]
    public async Task GetDetailAsync_SettlementNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.GetDetailAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Settlement not found.");
    }

    [Fact]
    public async Task GetDetailAsync_ActivityNotFound_ThrowsValidationException()
    {
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = Guid.NewGuid(), // non-existent activity
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.GetDetailAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Activity not found.");
    }

    [Fact]
    public async Task GetDetailAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync();
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.GetDetailAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetDetailAsync_ValidSettlement_ReturnsDetail()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetDetailAsync(settlementId, _callerId, CT);

        result.Should().NotBeNull();
        result.Id.Should().Be(settlementId);
        result.Amount.Should().Be(50);
    }

    // ── ConfirmSentAsync ──

    [Fact]
    public async Task ConfirmSentAsync_SettlementNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.ConfirmSentAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Settlement not found.");
    }

    [Fact]
    public async Task ConfirmSentAsync_CallerNotPayerFamily_ThrowsForbiddenException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id, // caller is in _familyId, not payer
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.ConfirmSentAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*paying family*");
    }

    [Fact]
    public async Task ConfirmSentAsync_StatusNotProposed_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _familyId, // caller is payer
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.PayerSent,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.ConfirmSentAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*expected Proposed*");
    }

    [Fact]
    public async Task ConfirmSentAsync_ValidProposed_UpdatesStatusAndCreatesApprovalStep()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ConfirmSentAsync(settlementId, _callerId, CT);

        result.Should().NotBeNull();
        var settlement = await _db.Settlements.FindAsync([settlementId], CT);
        settlement!.Status.Should().Be(SettlementStatus.PayerSent);

        var step = await _db.ApprovalSteps.FirstOrDefaultAsync(s => s.SettlementId == settlementId, CT);
        step.Should().NotBeNull();
        step!.StepType.Should().Be(StepType.PayerSent);
        step.ApproverId.Should().Be(_callerId);

        _notificationsMock.Verify(n => n.NotifyFamilyAsync(
            _family2Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── ConfirmReceivedAsync ──

    [Fact]
    public async Task ConfirmReceivedAsync_SettlementNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.ConfirmReceivedAsync(Guid.NewGuid(), _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Settlement not found.");
    }

    [Fact]
    public async Task ConfirmReceivedAsync_CallerNotReceiverFamily_ThrowsForbiddenException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id, // caller is in _familyId, not receiver
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.PayerSent,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.ConfirmReceivedAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*receiving family*");
    }

    [Fact]
    public async Task ConfirmReceivedAsync_StatusNotPayerSent_ThrowsValidationException()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId, // caller is receiver
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.ConfirmReceivedAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*expected PayerSent*");
    }

    [Fact]
    public async Task ConfirmReceivedAsync_ValidPayerSent_CompletesSettlement()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.PayerSent,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ConfirmReceivedAsync(settlementId, _callerId, CT);

        result.Should().NotBeNull();
        var settlement = await _db.Settlements.FindAsync([settlementId], CT);
        settlement!.Status.Should().Be(SettlementStatus.Completed);
        settlement.CompletedAt.Should().NotBeNull();

        var step = await _db.ApprovalSteps.FirstOrDefaultAsync(s => s.SettlementId == settlementId, CT);
        step.Should().NotBeNull();
        step!.StepType.Should().Be(StepType.ReceiverConfirmed);

        _notificationsMock.Verify(n => n.NotifyFamilyAsync(
            _family2Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmReceivedAsync_AllSettlementsCompleted_MarksActivitySettled()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.PayerSent,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.ConfirmReceivedAsync(settlementId, _callerId, CT);

        var activity = await _db.Activities.FindAsync([_activityId], CT);
        activity!.Status.Should().Be(ActivityStatus.Settled);
    }

    [Fact]
    public async Task ConfirmReceivedAsync_NotAllSettlementsCompleted_ActivityRemainsUnchanged()
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync(ActivityStatus.Closed);

        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.PayerSent,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        _db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 30,
            Currency = "EUR",
            Status = SettlementStatus.Proposed, // not completed
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.ConfirmReceivedAsync(settlementId, _callerId, CT);

        var activity = await _db.Activities.FindAsync([_activityId], CT);
        activity!.Status.Should().Be(ActivityStatus.Closed); // not changed to Settled
    }

    [Fact]
    public async Task ConfirmSentAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _familyId,
            ReceiverFamilyId = _family2Id,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.Proposed,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.ConfirmSentAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ConfirmReceivedAsync_CallerNotGroupMember_ThrowsForbiddenException()
    {
        await SeedActivityAsync(ActivityStatus.Closed);
        var settlementId = Guid.NewGuid();
        _db.Settlements.Add(new Settlement
        {
            Id = settlementId,
            ActivityId = _activityId,
            PayerFamilyId = _family2Id,
            ReceiverFamilyId = _familyId,
            Amount = 50,
            Currency = "EUR",
            Status = SettlementStatus.PayerSent,
            ProposedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        Func<Task> act = () => _sut.ConfirmReceivedAsync(settlementId, _callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
