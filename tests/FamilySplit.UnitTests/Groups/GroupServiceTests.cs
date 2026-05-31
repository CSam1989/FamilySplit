using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Groups;
using FamilySplit.Application.Groups.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Groups;

public class GroupServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GroupService _sut;
    private readonly Mock<ILogger<GroupService>> _logger = new();

    private CancellationToken CT => TestContext.Current.CancellationToken;

    public GroupServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new GroupService(
            _db,
            new CreateGroupValidator(),
            new UpdateGroupValidator(),
            new JoinGroupValidator(),
            _logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (Family family, FamilyMember admin) SeedFamilyWithAdmin(Guid? userId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = "Test Family",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var admin = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = uid,
            IsAdmin = true,
            DisplayName = "Admin",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Families.Add(family);
        _db.FamilyMembers.Add(admin);
        _db.SaveChanges();
        return (family, admin);
    }

    private Group SeedGroup(Guid createdByUserId)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = "Test Group",
            Description = "Desc",
            InviteCode = "ABCD1234",
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Groups.Add(group);
        _db.SaveChanges();
        return group;
    }

    private GroupFamily SeedGroupFamily(Guid groupId, Guid familyId, MemberRole role = MemberRole.Admin)
    {
        var gf = new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            FamilyId = familyId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        };
        _db.GroupFamilies.Add(gf);
        _db.SaveChanges();
        return gf;
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidDependencies_CreatesInstance()
    {
        _sut.Should().NotBeNull();
    }

    // ── ListAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_NoFamilyMember_ThrowsForbidden()
    {
        var callerId = Guid.NewGuid();

        var act = () => _sut.ListAsync(callerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListAsync_NoGroups_ReturnsEmptyList()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.ListAsync(admin.UserId!.Value, CT);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_WithGroups_ReturnsSummaries()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id);

        var result = await _sut.ListAsync(admin.UserId!.Value, CT);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(group.Id);
        result[0].Name.Should().Be(group.Name);
        result[0].FamilyCount.Should().Be(1);
        result[0].CallerFamilyRole.Should().Be(MemberRole.Admin);
    }

    // ── GetDetailAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetailAsync_NoFamilyMember_ThrowsForbidden()
    {
        var act = () => _sut.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetDetailAsync_NotMemberOfGroup_ThrowsForbidden()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        // No GroupFamily link

        var act = () => _sut.GetDetailAsync(group.Id, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetDetailAsync_MemberOfGroup_ReturnsDetail()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id);

        var result = await _sut.GetDetailAsync(group.Id, admin.UserId!.Value, CT);

        result.Id.Should().Be(group.Id);
        result.Name.Should().Be(group.Name);
        result.CallerFamilyRole.Should().Be(MemberRole.Admin);
        result.Families.Should().HaveCount(1);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesGroupAndReturnsDetail()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var req = new CreateGroupRequest("New Group", "A description");

        var result = await _sut.CreateAsync(req, admin.UserId!.Value, CT);

        result.Name.Should().Be("New Group");
        result.Description.Should().Be("A description");
        result.CallerFamilyRole.Should().Be(MemberRole.Admin);
        result.Families.Should().HaveCount(1);

        var dbGroup = await _db.Groups.FirstAsync(CT);
        dbGroup.Name.Should().Be("New Group");
    }

    [Fact]
    public async Task CreateAsync_TrimsNameAndDescription()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var req = new CreateGroupRequest("  Trimmed  ", "  Desc  ");

        var result = await _sut.CreateAsync(req, admin.UserId!.Value, CT);

        result.Name.Should().Be("Trimmed");
        result.Description.Should().Be("Desc");
    }

    [Fact]
    public async Task CreateAsync_NullDescription_Allowed()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var req = new CreateGroupRequest("Group", null);

        var result = await _sut.CreateAsync(req, admin.UserId!.Value, CT);

        result.Description.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_NotFamilyAdmin_ThrowsForbidden()
    {
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = "Fam",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = Guid.NewGuid(),
            IsAdmin = false,
            DisplayName = "Member",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Families.Add(family);
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.CreateAsync(new CreateGroupRequest("G", null), member.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationException()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var req = new CreateGroupRequest("", null); // empty name

        var act = () => _sut.CreateAsync(req, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesGroup()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id);
        var req = new UpdateGroupRequest("Updated Name", "Updated Desc");

        var result = await _sut.UpdateAsync(group.Id, req, admin.UserId!.Value, CT);

        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Desc");
    }

    [Fact]
    public async Task UpdateAsync_NotAdmin_ThrowsForbidden()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id, MemberRole.Member);

        var act = () => _sut.UpdateAsync(group.Id, new UpdateGroupRequest("X", null), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_InvalidRequest_ThrowsValidationException()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id);

        var act = () => _sut.UpdateAsync(group.Id, new UpdateGroupRequest("", null), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_TrimsValues()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id);

        var result = await _sut.UpdateAsync(group.Id, new UpdateGroupRequest("  Trimmed  ", "  D  "), admin.UserId!.Value, CT);

        result.Name.Should().Be("Trimmed");
        result.Description.Should().Be("D");
    }

    // ── JoinAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinAsync_ValidCode_JoinsGroupAndReturnsDetail()
    {
        var (family1, admin1) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin1.UserId!.Value);
        SeedGroupFamily(group.Id, family1.Id);

        var (_, admin2) = SeedFamilyWithAdmin();
        var req = new JoinGroupRequest(group.InviteCode);

        var result = await _sut.JoinAsync(req, admin2.UserId!.Value, CT);

        result.Id.Should().Be(group.Id);
        result.CallerFamilyRole.Should().Be(MemberRole.Member);
        result.Families.Should().HaveCount(2);
    }

    [Fact]
    public async Task JoinAsync_LowercaseCode_MatchesCaseInsensitively()
    {
        var (family1, admin1) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin1.UserId!.Value);
        SeedGroupFamily(group.Id, family1.Id);

        var (_, admin2) = SeedFamilyWithAdmin();
        var req = new JoinGroupRequest(group.InviteCode.ToLowerInvariant());

        var result = await _sut.JoinAsync(req, admin2.UserId!.Value, CT);

        result.Id.Should().Be(group.Id);
    }

    [Fact]
    public async Task JoinAsync_InvalidCode_ThrowsValidationException()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var req = new JoinGroupRequest("INVALIDCODE");

        var act = () => _sut.JoinAsync(req, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task JoinAsync_AlreadyMember_ThrowsValidationException()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id);
        var req = new JoinGroupRequest(group.InviteCode);

        var act = () => _sut.JoinAsync(req, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task JoinAsync_NotFamilyAdmin_ThrowsForbidden()
    {
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = "Fam",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = Guid.NewGuid(),
            IsAdmin = false,
            DisplayName = "Member",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Families.Add(family);
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.JoinAsync(new JoinGroupRequest("ABCD1234"), member.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task JoinAsync_NoFamilyMember_ThrowsForbidden()
    {
        var act = () => _sut.JoinAsync(new JoinGroupRequest("ABCD1234"), Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── LeaveAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveAsync_MemberRole_LeavesSuccessfully()
    {
        var (family1, admin1) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin1.UserId!.Value);
        SeedGroupFamily(group.Id, family1.Id);

        var (family2, admin2) = SeedFamilyWithAdmin();
        SeedGroupFamily(group.Id, family2.Id, MemberRole.Member);

        await _sut.LeaveAsync(group.Id, admin2.UserId!.Value, CT);

        var remaining = await _db.GroupFamilies.Where(gf => gf.GroupId == group.Id).ToListAsync(CT);
        remaining.Should().HaveCount(1);
        remaining[0].FamilyId.Should().Be(family1.Id);
    }

    [Fact]
    public async Task LeaveAsync_SoleAdmin_ThrowsValidationException()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id, MemberRole.Admin);

        var act = () => _sut.LeaveAsync(group.Id, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task LeaveAsync_AdminWithOtherAdmins_LeavesSuccessfully()
    {
        var (family1, admin1) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin1.UserId!.Value);
        SeedGroupFamily(group.Id, family1.Id, MemberRole.Admin);

        var (family2, admin2) = SeedFamilyWithAdmin();
        SeedGroupFamily(group.Id, family2.Id, MemberRole.Admin);

        await _sut.LeaveAsync(group.Id, admin1.UserId!.Value, CT);

        var remaining = await _db.GroupFamilies.Where(gf => gf.GroupId == group.Id).ToListAsync(CT);
        remaining.Should().HaveCount(1);
        remaining[0].FamilyId.Should().Be(family2.Id);
    }

    [Fact]
    public async Task LeaveAsync_NotMemberOfGroup_ThrowsValidationException()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var act = () => _sut.LeaveAsync(Guid.NewGuid(), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task LeaveAsync_NotFamilyAdmin_ThrowsForbidden()
    {
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = "Fam",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = Guid.NewGuid(),
            IsAdmin = false,
            DisplayName = "Member",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Families.Add(family);
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.LeaveAsync(Guid.NewGuid(), member.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── RegenerateInviteCodeAsync ───────────────────────────────────────────

    [Fact]
    public async Task RegenerateInviteCodeAsync_Admin_ReturnsNewCode()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        var oldCode = group.InviteCode;
        SeedGroupFamily(group.Id, family.Id, MemberRole.Admin);

        var newCode = await _sut.RegenerateInviteCodeAsync(group.Id, admin.UserId!.Value, CT);

        newCode.Should().NotBeNullOrWhiteSpace();
        newCode.Should().NotBe(oldCode);

        var dbGroup = await _db.Groups.FindAsync([group.Id], CT);
        dbGroup!.InviteCode.Should().Be(newCode);
    }

    [Fact]
    public async Task RegenerateInviteCodeAsync_NotAdmin_ThrowsForbidden()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id, MemberRole.Member);

        var act = () => _sut.RegenerateInviteCodeAsync(group.Id, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RegenerateInviteCodeAsync_GroupNotFound_ThrowsNotFoundException()
    {
        var (family, admin) = SeedFamilyWithAdmin();
        var group = SeedGroup(admin.UserId!.Value);
        SeedGroupFamily(group.Id, family.Id, MemberRole.Admin);

        var act = () => _sut.RegenerateInviteCodeAsync(Guid.NewGuid(), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
