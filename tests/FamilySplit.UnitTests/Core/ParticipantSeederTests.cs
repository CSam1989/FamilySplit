using FamilySplit.Application.Core;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.UnitTests.Core;

public class ParticipantSeederTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ParticipantSeeder _sut;

    public ParticipantSeederTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new ParticipantSeeder(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void Constructor_WithDbContext_DoesNotThrow()
    {
        _sut.Should().NotBeNull();
    }

    [Fact]
    public async Task SeedForActivityAsync_NoFamiliesInGroup_AddsNoParticipants()
    {
        var activity = new Activity { Id = Guid.NewGuid(), GroupId = Guid.NewGuid(), Name = "Test" };

        await _sut.SeedForActivityAsync(activity);

        _db.ChangeTracker.Entries<ActivityParticipant>().Should().BeEmpty();
    }

    [Fact]
    public async Task SeedForActivityAsync_ActiveMembers_AddsParticipantsForEach()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var member1 = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, DisplayName = "A", IsActive = true };
        var member2 = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, DisplayName = "B", IsActive = true };

        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = groupId, FamilyId = familyId, Role = MemberRole.Admin });
        _db.FamilyMembers.AddRange(member1, member2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activity = new Activity { Id = Guid.NewGuid(), GroupId = groupId, Name = "Test" };

        await _sut.SeedForActivityAsync(activity);

        var participants = _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        participants.Should().HaveCount(2);
        participants.Select(p => p.FamilyMemberId).Should().BeEquivalentTo([member1.Id, member2.Id]);
        participants.Should().AllSatisfy(p => p.ActivityId.Should().Be(activity.Id));
    }

    [Fact]
    public async Task SeedForActivityAsync_InactiveMembersExcluded()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var active = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, DisplayName = "A", IsActive = true };
        var inactive = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, DisplayName = "B", IsActive = false };

        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = groupId, FamilyId = familyId, Role = MemberRole.Admin });
        _db.FamilyMembers.AddRange(active, inactive);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activity = new Activity { Id = Guid.NewGuid(), GroupId = groupId, Name = "Test" };
        await _sut.SeedForActivityAsync(activity);

        var participants = _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        participants.Should().ContainSingle()
            .Which.FamilyMemberId.Should().Be(active.Id);
    }

    [Fact]
    public async Task SeedForActivityAsync_MultipleFamilies_AddsAllActiveMembers()
    {
        var groupId = Guid.NewGuid();
        var family1 = Guid.NewGuid();
        var family2 = Guid.NewGuid();
        var m1 = new FamilyMember { Id = Guid.NewGuid(), FamilyId = family1, DisplayName = "A", IsActive = true };
        var m2 = new FamilyMember { Id = Guid.NewGuid(), FamilyId = family2, DisplayName = "B", IsActive = true };

        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = groupId, FamilyId = family1, Role = MemberRole.Admin });
        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = groupId, FamilyId = family2, Role = MemberRole.Member });
        _db.FamilyMembers.AddRange(m1, m2);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activity = new Activity { Id = Guid.NewGuid(), GroupId = groupId, Name = "Test" };
        await _sut.SeedForActivityAsync(activity);

        var added = _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        added.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeedForActivityAsync_MembersFromOtherGroup_NotIncluded()
    {
        var groupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var member = new FamilyMember { Id = Guid.NewGuid(), FamilyId = familyId, DisplayName = "A", IsActive = true };

        _db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = otherGroupId, FamilyId = familyId, Role = MemberRole.Admin });
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activity = new Activity { Id = Guid.NewGuid(), GroupId = groupId, Name = "Test" };
        await _sut.SeedForActivityAsync(activity);

        _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SeedForSubActivityAsync_CopiesParentParticipants()
    {
        var parentActivityId = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = parentActivityId, FamilyMemberId = m1 });
        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = parentActivityId, FamilyMemberId = m2 });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var subActivity = new Activity { Id = Guid.NewGuid(), GroupId = Guid.NewGuid(), Name = "Sub" };
        await _sut.SeedForSubActivityAsync(subActivity, parentActivityId);

        var added = _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        added.Should().HaveCount(2);
        added.Select(p => p.FamilyMemberId).Should().BeEquivalentTo([m1, m2]);
        added.Should().AllSatisfy(p => p.ActivityId.Should().Be(subActivity.Id));
    }

    [Fact]
    public async Task SeedForSubActivityAsync_NoParentParticipants_AddsNothing()
    {
        var subActivity = new Activity { Id = Guid.NewGuid(), GroupId = Guid.NewGuid(), Name = "Sub" };

        await _sut.SeedForSubActivityAsync(subActivity, Guid.NewGuid());

        _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SeedForSubActivityAsync_DoesNotIncludeOtherActivityParticipants()
    {
        var parentId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = parentId, FamilyMemberId = m1 });
        _db.ActivityParticipants.Add(new ActivityParticipant { Id = Guid.NewGuid(), ActivityId = otherId, FamilyMemberId = m2 });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var subActivity = new Activity { Id = Guid.NewGuid(), GroupId = Guid.NewGuid(), Name = "Sub" };
        await _sut.SeedForSubActivityAsync(subActivity, parentId);

        var added = _db.ChangeTracker.Entries<ActivityParticipant>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        added.Should().ContainSingle().Which.FamilyMemberId.Should().Be(m1);
    }
}
