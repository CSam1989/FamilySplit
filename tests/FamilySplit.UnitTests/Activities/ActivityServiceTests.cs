using FamilySplit.Application.Activities;
using FamilySplit.Application.Activities.Dtos;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Activities;

public class ActivityServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ActivityService _sut;
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _familyId = Guid.NewGuid();

    public ActivityServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _sut = new ActivityService(
            _db,
            new CreateActivityValidator(),
            new UpdateActivityValidator(),
            new AddParticipantValidator(),
            new ParticipantSeeder(_db),
            NullLogger<ActivityService>.Instance);

        _db.Families.Add(new Family { Id = _familyId, Name = "TestFamily" });
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = _familyId,
            UserId = _callerId,
            DisplayName = "Caller",
            IsActive = true,
        });
        _db.GroupFamilies.Add(new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = _groupId,
            FamilyId = _familyId,
            Role = MemberRole.Admin,
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private CancellationToken CT => TestContext.Current.CancellationToken;

    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_CreatesInstance()
    {
        _sut.Should().NotBeNull();
    }

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_NotGroupMember_ThrowsForbidden()
    {
        var outsider = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ListAsync(_groupId, outsider, CT));
    }

    [Fact]
    public async Task ListAsync_NoActivities_ReturnsEmptyList()
    {
        var result = await _sut.ListAsync(_groupId, _callerId, CT);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_WithActivities_ReturnsSummaries()
    {
        _db.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            GroupId = _groupId,
            Name = "Trip",
            Status = ActivityStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_groupId, _callerId, CT);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Trip");
        result[0].ExpenseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task ListAsync_ExcludesSubActivities()
    {
        var parentId = Guid.NewGuid();
        _db.Activities.AddRange(
            new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow },
            new Activity { Id = Guid.NewGuid(), GroupId = _groupId, Name = "Sub", ParentActivityId = parentId, Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_groupId, _callerId, CT);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Parent");
        result[0].SubActivityCount.Should().Be(1);
    }

    [Fact]
    public async Task ListAsync_IncludesExpenseAggregates()
    {
        var actId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "A", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        _db.Expenses.AddRange(
            new Expense { Id = Guid.NewGuid(), ActivityId = actId, TotalAmount = 10m, Currency = "USD", Title = "E1" },
            new Expense { Id = Guid.NewGuid(), ActivityId = actId, TotalAmount = 20m, Currency = "USD", Title = "E2" });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_groupId, _callerId, CT);

        result[0].ExpenseCount.Should().Be(2);
        result[0].TotalExpenseAmount.Should().Be(30m);
        result[0].ExpenseCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task ListAsync_IncludesParticipantCounts()
    {
        var actId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "A", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = actId, FamilyMemberId = Guid.NewGuid() });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.ListAsync(_groupId, _callerId, CT);

        result[0].ParticipantCount.Should().Be(1);
    }

    // ── GetDetailAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetailAsync_NotFound_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _sut.GetDetailAsync(Guid.NewGuid(), _callerId, CT));
    }

    [Fact]
    public async Task GetDetailAsync_NotGroupMember_ThrowsForbidden()
    {
        var actId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "A", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetDetailAsync(actId, Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task GetDetailAsync_ValidActivity_ReturnsDetail()
    {
        var actId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = actId, GroupId = _groupId, Name = "Test", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetDetailAsync(actId, _callerId, CT);

        result.Id.Should().Be(actId);
        result.Name.Should().Be("Test");
        result.Status.Should().Be(ActivityStatus.Open);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesActivity()
    {
        var req = new CreateActivityRequest("My Activity", "Desc");

        var result = await _sut.CreateAsync(_groupId, req, _callerId, CT);

        result.Name.Should().Be("My Activity");
        result.Description.Should().Be("Desc");
        result.GroupId.Should().Be(_groupId);
        result.Status.Should().Be(ActivityStatus.Open);
        _db.Activities.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_TrimsNameAndDescription()
    {
        var req = new CreateActivityRequest("  Trimmed  ", "  Desc  ");

        var result = await _sut.CreateAsync(_groupId, req, _callerId, CT);

        result.Name.Should().Be("Trimmed");
        result.Description.Should().Be("Desc");
    }

    [Fact]
    public async Task CreateAsync_NotGroupMember_ThrowsForbidden()
    {
        var req = new CreateActivityRequest("A", null);
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateAsync(_groupId, req, Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsValidation()
    {
        var req = new CreateActivityRequest("", null);
        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateAsync(_groupId, req, _callerId, CT));
    }

    [Fact]
    public async Task CreateAsync_NullDescription_AllowedAndStoredAsNull()
    {
        var req = new CreateActivityRequest("NoDesc", null);

        var result = await _sut.CreateAsync(_groupId, req, _callerId, CT);

        result.Description.Should().BeNull();
    }

    // ── CreateSubActivityAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateSubActivityAsync_ValidRequest_CreatesSubActivity()
    {
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var req = new CreateActivityRequest("Sub", null);
        var result = await _sut.CreateSubActivityAsync(parentId, req, _callerId, CT);

        result.Name.Should().Be("Sub");
        result.ParentActivityId.Should().Be(parentId);
    }

    [Fact]
    public async Task CreateSubActivityAsync_ParentNotFound_ThrowsValidation()
    {
        var req = new CreateActivityRequest("Sub", null);
        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateSubActivityAsync(Guid.NewGuid(), req, _callerId, CT));
    }

    [Fact]
    public async Task CreateSubActivityAsync_NestedTooDeep_ThrowsValidation()
    {
        var parentId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        _db.Activities.AddRange(
            new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow },
            new Activity { Id = subId, GroupId = _groupId, Name = "Sub", ParentActivityId = parentId, Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var req = new CreateActivityRequest("SubSub", null);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateSubActivityAsync(subId, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "ParentActivityId");
    }

    [Fact]
    public async Task CreateSubActivityAsync_ClosedParent_ThrowsValidation()
    {
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Closed, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var req = new CreateActivityRequest("Sub", null);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateSubActivityAsync(parentId, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task CreateSubActivityAsync_SettledParent_ThrowsValidation()
    {
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Settled, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var req = new CreateActivityRequest("Sub", null);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateSubActivityAsync(parentId, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task CreateSubActivityAsync_NotGroupMember_ThrowsForbidden()
    {
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var req = new CreateActivityRequest("Sub", null);
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateSubActivityAsync(parentId, req, Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task CreateSubActivityAsync_EmptyName_ThrowsValidation()
    {
        var parentId = Guid.NewGuid();
        _db.Activities.Add(new Activity { Id = parentId, GroupId = _groupId, Name = "Parent", Status = ActivityStatus.Open, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(CT);

        var req = new CreateActivityRequest("", null);
        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateSubActivityAsync(parentId, req, _callerId, CT));
    }

    // ── UpdateAsync ─────────────────────────────────────────────────────────

    private Activity SeedOpenActivity(Guid? id = null)
    {
        var activity = new Activity
        {
            Id = id ?? Guid.NewGuid(),
            GroupId = _groupId,
            Name = "Original",
            Description = "OrigDesc",
            Status = ActivityStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Activities.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesActivity()
    {
        var activity = SeedOpenActivity();
        var req = new UpdateActivityRequest("Updated", "NewDesc");

        var result = await _sut.UpdateAsync(activity.Id, req, _callerId, CT);

        result.Name.Should().Be("Updated");
        result.Description.Should().Be("NewDesc");
    }

    [Fact]
    public async Task UpdateAsync_TrimsNameAndDescription()
    {
        var activity = SeedOpenActivity();
        var req = new UpdateActivityRequest("  Trimmed  ", "  Desc  ");

        var result = await _sut.UpdateAsync(activity.Id, req, _callerId, CT);

        result.Name.Should().Be("Trimmed");
        result.Description.Should().Be("Desc");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsValidation()
    {
        var req = new UpdateActivityRequest("X", null);
        await Assert.ThrowsAsync<ValidationException>(() => _sut.UpdateAsync(Guid.NewGuid(), req, _callerId, CT));
    }

    [Fact]
    public async Task UpdateAsync_NotGroupMember_ThrowsForbidden()
    {
        var activity = SeedOpenActivity();
        var req = new UpdateActivityRequest("X", null);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.UpdateAsync(activity.Id, req, Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task UpdateAsync_ClosedActivity_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        activity.Status = ActivityStatus.Closed;
        await _db.SaveChangesAsync(CT);

        var req = new UpdateActivityRequest("X", null);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.UpdateAsync(activity.Id, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task UpdateAsync_EmptyName_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        var req = new UpdateActivityRequest("", null);

        await Assert.ThrowsAsync<ValidationException>(() => _sut.UpdateAsync(activity.Id, req, _callerId, CT));
    }

    [Fact]
    public async Task UpdateAsync_NullDescription_SetsNull()
    {
        var activity = SeedOpenActivity();
        var req = new UpdateActivityRequest("Name", null);

        var result = await _sut.UpdateAsync(activity.Id, req, _callerId, CT);

        result.Description.Should().BeNull();
    }

    // ── CloseAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_ValidTopLevel_ClosesActivity()
    {
        var activity = SeedOpenActivity();

        var result = await _sut.CloseAsync(activity.Id, _callerId, CT);

        result.Status.Should().Be(ActivityStatus.Closed);
        result.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CloseAsync_NotFound_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _sut.CloseAsync(Guid.NewGuid(), _callerId, CT));
    }

    [Fact]
    public async Task CloseAsync_NotGroupMember_ThrowsForbidden()
    {
        var activity = SeedOpenActivity();
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CloseAsync(activity.Id, Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task CloseAsync_AlreadyClosed_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        activity.Status = ActivityStatus.Closed;
        await _db.SaveChangesAsync(CT);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.CloseAsync(activity.Id, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task CloseAsync_SubActivity_ThrowsValidation()
    {
        var parent = SeedOpenActivity();
        var sub = new Activity
        {
            Id = Guid.NewGuid(),
            GroupId = _groupId,
            Name = "Sub",
            Status = ActivityStatus.Open,
            ParentActivityId = parent.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Activities.Add(sub);
        await _db.SaveChangesAsync(CT);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.CloseAsync(sub.Id, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task CloseAsync_AbsorbsOpenSubActivities()
    {
        var parent = SeedOpenActivity();
        var subId = Guid.NewGuid();
        _db.Activities.Add(new Activity
        {
            Id = subId,
            GroupId = _groupId,
            Name = "Sub",
            Status = ActivityStatus.Open,
            ParentActivityId = parent.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        await _sut.CloseAsync(parent.Id, _callerId, CT);

        var sub = await _db.Activities.FindAsync([subId], CT);
        sub!.Status.Should().Be(ActivityStatus.AbsorbedByParent);
        sub.ClosedAt.Should().NotBeNull();
        sub.ClosedByUserId.Should().Be(_callerId);
    }

    // ── AddParticipantAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AddParticipantAsync_ValidMember_AddsParticipant()
    {
        var activity = SeedOpenActivity();
        var memberId = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember { Id = memberId, FamilyId = _familyId, UserId = Guid.NewGuid(), DisplayName = "M", IsActive = true });
        await _db.SaveChangesAsync(CT);

        var req = new AddParticipantRequest(memberId);
        var result = await _sut.AddParticipantAsync(activity.Id, req, _callerId, CT);

        result.Should().NotBeNull();
        _db.ActivityParticipants.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddParticipantAsync_NotFound_ThrowsValidation()
    {
        var req = new AddParticipantRequest(Guid.NewGuid());
        await Assert.ThrowsAsync<ValidationException>(() => _sut.AddParticipantAsync(Guid.NewGuid(), req, _callerId, CT));
    }

    [Fact]
    public async Task AddParticipantAsync_NotGroupMember_ThrowsForbidden()
    {
        var activity = SeedOpenActivity();
        var req = new AddParticipantRequest(Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.AddParticipantAsync(activity.Id, req, Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task AddParticipantAsync_ClosedActivity_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        activity.Status = ActivityStatus.Closed;
        await _db.SaveChangesAsync(CT);

        var req = new AddParticipantRequest(Guid.NewGuid());
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.AddParticipantAsync(activity.Id, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task AddParticipantAsync_MemberNotInGroup_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        var otherMemberId = Guid.NewGuid();
        var otherFamilyId = Guid.NewGuid();
        _db.Families.Add(new Family { Id = otherFamilyId, Name = "Other" });
        _db.FamilyMembers.Add(new FamilyMember { Id = otherMemberId, FamilyId = otherFamilyId, UserId = Guid.NewGuid(), DisplayName = "O", IsActive = true });
        await _db.SaveChangesAsync(CT);

        var req = new AddParticipantRequest(otherMemberId);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.AddParticipantAsync(activity.Id, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "FamilyMemberId");
    }

    [Fact]
    public async Task AddParticipantAsync_AlreadyParticipant_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        var memberId = Guid.NewGuid();
        _db.FamilyMembers.Add(new FamilyMember { Id = memberId, FamilyId = _familyId, UserId = Guid.NewGuid(), DisplayName = "M", IsActive = true });
        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = activity.Id, FamilyMemberId = memberId });
        await _db.SaveChangesAsync(CT);

        var req = new AddParticipantRequest(memberId);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.AddParticipantAsync(activity.Id, req, _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "FamilyMemberId");
    }

    // ── RemoveParticipantAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RemoveParticipantAsync_ValidParticipant_Removes()
    {
        var activity = SeedOpenActivity();
        var memberId = Guid.NewGuid();
        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = activity.Id, FamilyMemberId = memberId });
        await _db.SaveChangesAsync(CT);

        var result = await _sut.RemoveParticipantAsync(activity.Id, memberId, _callerId, CT);

        result.Should().NotBeNull();
        _db.ActivityParticipants.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveParticipantAsync_NotFound_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _sut.RemoveParticipantAsync(Guid.NewGuid(), Guid.NewGuid(), _callerId, CT));
    }

    [Fact]
    public async Task RemoveParticipantAsync_NotGroupMember_ThrowsForbidden()
    {
        var activity = SeedOpenActivity();
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.RemoveParticipantAsync(activity.Id, Guid.NewGuid(), Guid.NewGuid(), CT));
    }

    [Fact]
    public async Task RemoveParticipantAsync_ClosedActivity_ThrowsValidation()
    {
        var activity = SeedOpenActivity();
        activity.Status = ActivityStatus.Closed;
        await _db.SaveChangesAsync(CT);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.RemoveParticipantAsync(activity.Id, Guid.NewGuid(), _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task RemoveParticipantAsync_NotParticipant_ThrowsValidation()
    {
        var activity = SeedOpenActivity();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.RemoveParticipantAsync(activity.Id, Guid.NewGuid(), _callerId, CT));
        ex.Errors.Should().Contain(e => e.PropertyName == "FamilyMemberId");
    }
}
