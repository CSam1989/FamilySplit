using FamilySplit.Application.Dashboard;
using FamilySplit.Application.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.UnitTests.Dashboard;

public class DashboardServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DashboardService _sut;

    private CancellationToken CT => TestContext.Current.CancellationToken;

    // Shared IDs
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new DashboardService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task SeedCallerAsync()
    {
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            UserId = _callerId,
            IsActive = true,
            DisplayName = "Caller",
        });
        await _db.SaveChangesAsync(CT);
    }

    private async Task SeedGroupAsync(Guid? groupId = null, string name = "Group1")
    {
        var gid = groupId ?? _groupId;
        _db.Groups.Add(new Group
        {
            Id = gid,
            Name = name,
            InviteCode = "ABC",
            CreatedByUserId = _callerId,
        });
        _db.GroupFamilies.Add(new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = gid,
            FamilyId = _familyId,
        });
        await _db.SaveChangesAsync(CT);
    }

    [Fact]
    public async Task GetStatsAsync_NoFamilyMembership_ThrowsForbidden()
    {
        var act = () => _sut.GetStatsAsync(Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetStatsAsync_NoGroups_ReturnsEmptyList()
    {
        await SeedCallerAsync();

        var result = await _sut.GetStatsAsync(_callerId, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatsAsync_GroupWithNoActivities_ReturnsZeroStats()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var result = await _sut.GetStatsAsync(_callerId, CT);

        result.Should().HaveCount(1);
        var stat = result[0];
        stat.GroupId.Should().Be(_groupId);
        stat.GroupName.Should().Be("Group1");
        stat.TotalActivities.Should().Be(0);
        stat.TotalGroupSpend.Should().Be(0);
        stat.Currency.Should().Be("EUR");
        stat.NetBalance.Should().Be(0);
        stat.PendingSettlements.Should().Be(0);
        stat.LatestActivityName.Should().BeNull();
    }

    [Fact]
    public async Task GetStatsAsync_WithActivities_ReturnsCorrectCounts()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        _db.Activities.AddRange(
            new Activity { Id = Guid.NewGuid(), GroupId = _groupId, Name = "A1", Status = ActivityStatus.Open, CreatedByUserId = _callerId, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new Activity { Id = Guid.NewGuid(), GroupId = _groupId, Name = "A2", Status = ActivityStatus.Closed, CreatedByUserId = _callerId, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new Activity { Id = Guid.NewGuid(), GroupId = _groupId, Name = "A3", Status = ActivityStatus.Settled, CreatedByUserId = _callerId, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        var stat = result.Single();
        stat.TotalActivities.Should().Be(3);
        stat.OpenActivities.Should().Be(1);
        stat.ClosedActivities.Should().Be(1);
        stat.SettledActivities.Should().Be(1);
        stat.LatestActivityName.Should().Be("A3");
        stat.LatestActivityStatus.Should().Be("Settled");
    }

    [Fact]
    public async Task GetStatsAsync_WithExpenses_ReturnsTotalSpendAndShare()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var actId = Guid.NewGuid();
        var memberId = _db.FamilyMembers.First().Id;
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "Trip", Status = ActivityStatus.Open, CreatedByUserId = _callerId });

        var expId = Guid.NewGuid();
        _db.Expenses.Add(new Expense { Id = expId, ActivityId = actId, PaidByUserId = _callerId, Title = "Lunch", TotalAmount = 100m, Currency = "USD" });
        _db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expId, FamilyMemberId = memberId, CalculatedAmount = 50m, WeightSnapshot = 1m });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        var stat = result.Single();
        stat.TotalGroupSpend.Should().Be(100m);
        stat.MyFamilyShare.Should().Be(50m);
        stat.Currency.Should().Be("USD");
        stat.ActiveGroupSpend.Should().Be(100m);
        stat.ActiveFamilyShare.Should().Be(50m);
    }

    [Fact]
    public async Task GetStatsAsync_NetBalance_PaidMinusOwed()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var actId = Guid.NewGuid();
        var memberId = _db.FamilyMembers.First().Id;
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "Trip", Status = ActivityStatus.Open, CreatedByUserId = _callerId });

        var expId = Guid.NewGuid();
        _db.Expenses.Add(new Expense { Id = expId, ActivityId = actId, PaidByUserId = _callerId, Title = "Dinner", TotalAmount = 200m, Currency = "EUR" });
        _db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expId, FamilyMemberId = memberId, CalculatedAmount = 80m, WeightSnapshot = 1m });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        // paid=200, owed=80, balance=120
        result.Single().NetBalance.Should().Be(120m);
    }

    [Fact]
    public async Task GetStatsAsync_SettledActivities_ExcludedFromActiveSpendAndBalance()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var settledActId = Guid.NewGuid();
        var memberId = _db.FamilyMembers.First().Id;
        _db.Activities.Add(new Activity { Id = settledActId, GroupId = _groupId, Name = "Old", Status = ActivityStatus.Settled, CreatedByUserId = _callerId });

        var expId = Guid.NewGuid();
        _db.Expenses.Add(new Expense { Id = expId, ActivityId = settledActId, PaidByUserId = _callerId, Title = "X", TotalAmount = 500m });
        _db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expId, FamilyMemberId = memberId, CalculatedAmount = 250m, WeightSnapshot = 1m });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        var stat = result.Single();
        stat.TotalGroupSpend.Should().Be(500m); // historical includes settled
        stat.ActiveGroupSpend.Should().Be(0m);  // settled excluded
        stat.NetBalance.Should().Be(0m);         // settled excluded from balance
    }

    [Fact]
    public async Task GetStatsAsync_PendingSettlements_CountsCorrectly()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var actId = Guid.NewGuid();
        var otherFamilyId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "Trip", Status = ActivityStatus.Open, CreatedByUserId = _callerId });

        // Proposed where caller is payer -> pending
        _db.Settlements.Add(new Settlement { Id = Guid.NewGuid(), ActivityId = actId, PayerFamilyId = _familyId, ReceiverFamilyId = otherFamilyId, Amount = 50m, Status = SettlementStatus.Proposed });
        // PayerSent where caller is receiver -> pending
        _db.Settlements.Add(new Settlement { Id = Guid.NewGuid(), ActivityId = actId, PayerFamilyId = otherFamilyId, ReceiverFamilyId = _familyId, Amount = 30m, Status = SettlementStatus.PayerSent });
        // Completed -> not pending
        _db.Settlements.Add(new Settlement { Id = Guid.NewGuid(), ActivityId = actId, PayerFamilyId = _familyId, ReceiverFamilyId = otherFamilyId, Amount = 10m, Status = SettlementStatus.Completed });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        result.Single().PendingSettlements.Should().Be(2);
    }

    [Fact]
    public async Task GetStatsAsync_ExcludedParticipant_NotCountedInShare()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var actId = Guid.NewGuid();
        var memberId = _db.FamilyMembers.First().Id;
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "Trip", Status = ActivityStatus.Open, CreatedByUserId = _callerId });

        var expId = Guid.NewGuid();
        _db.Expenses.Add(new Expense { Id = expId, ActivityId = actId, PaidByUserId = _callerId, Title = "X", TotalAmount = 100m });
        _db.ExpenseParticipants.Add(new ExpenseParticipant { Id = Guid.NewGuid(), ExpenseId = expId, FamilyMemberId = memberId, CalculatedAmount = 50m, WeightSnapshot = 1m, IsExcluded = true });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        result.Single().MyFamilyShare.Should().Be(0m);
    }

    [Fact]
    public async Task GetStatsAsync_InactiveMembership_ThrowsForbidden()
    {
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            UserId = _callerId,
            IsActive = false,
            DisplayName = "Inactive",
        });
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.GetStatsAsync(_callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetStatsAsync_SubActivities_ExcludedFromTopLevel()
    {
        await SeedCallerAsync();
        await SeedGroupAsync();

        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Open, CreatedByUserId = _callerId });
        _db.Activities.Add(new Activity { Id = Guid.NewGuid(), GroupId = _groupId, Name = "Child", Status = ActivityStatus.Open, CreatedByUserId = _callerId, ParentActivityId = parentId });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetStatsAsync(_callerId, CT);

        result.Single().TotalActivities.Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_MultipleGroups_ReturnsStatsForEach()
    {
        await SeedCallerAsync();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        await SeedGroupAsync(g1, "Alpha");
        await SeedGroupAsync(g2, "Beta");

        var result = await _sut.GetStatsAsync(_callerId, CT);

        result.Should().HaveCount(2);
        result.Select(r => r.GroupName).Should().BeEquivalentTo(["Alpha", "Beta"]);
    }
}
