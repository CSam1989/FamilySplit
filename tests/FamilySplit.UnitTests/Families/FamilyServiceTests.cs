using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families;
using FamilySplit.Application.Families.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Families;

public class FamilyServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FamilyService _sut;
    private readonly Mock<ILogger<FamilyService>> _logger = new();

    private CancellationToken CT => TestContext.Current.CancellationToken;

    public FamilyServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new FamilyService(
            _db,
            new AddFamilyMemberValidator(),
            new UpdateFamilyMemberValidator(),
            new UpdateFamilyNameValidator(),
            _logger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

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

    // ── GetMyFamilyAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyFamilyAsync_NoMember_ReturnsNull()
    {
        var result = await _sut.GetMyFamilyAsync(Guid.NewGuid(), CT);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMyFamilyAsync_ActiveMember_ReturnsFamilyDto()
    {
        var (family, admin) = SeedFamilyWithAdmin();

        var result = await _sut.GetMyFamilyAsync(admin.UserId!.Value, CT);

        result.Should().NotBeNull();
        result!.Id.Should().Be(family.Id);
        result.Name.Should().Be("Test Family");
        result.Members.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyFamilyAsync_InactiveMember_ReturnsNull()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        admin.IsActive = false;
        await _db.SaveChangesAsync(CT);

        var result = await _sut.GetMyFamilyAsync(admin.UserId!.Value, CT);
        result.Should().BeNull();
    }

    // ── GetMyProfileAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyProfileAsync_NoMember_ReturnsNull()
    {
        var result = await _sut.GetMyProfileAsync(Guid.NewGuid(), CT);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMyProfileAsync_ActiveMember_ReturnsDto()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.GetMyProfileAsync(admin.UserId!.Value, CT);

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Admin");
        result.IsAdmin.Should().BeTrue();
        result.IsLinked.Should().BeTrue();
    }

    // ── UpdateFamilyNameAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateFamilyNameAsync_AdminCaller_UpdatesName()
    {
        var (family, admin) = SeedFamilyWithAdmin();

        var result = await _sut.UpdateFamilyNameAsync(
            new UpdateFamilyNameRequest("New Name"), admin.UserId!.Value, CT);

        result.Name.Should().Be("New Name");
        var updated = await _db.Families.FindAsync([family.Id], CT);
        updated!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateFamilyNameAsync_NonAdmin_ThrowsForbidden()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        admin.IsAdmin = false;
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.UpdateFamilyNameAsync(
            new UpdateFamilyNameRequest("New"), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateFamilyNameAsync_NoMember_ThrowsForbidden()
    {
        var act = () => _sut.UpdateFamilyNameAsync(
            new UpdateFamilyNameRequest("X"), Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateFamilyNameAsync_EmptyName_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var act = () => _sut.UpdateFamilyNameAsync(
            new UpdateFamilyNameRequest(""), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateFamilyNameAsync_TrimsWhitespace()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.UpdateFamilyNameAsync(
            new UpdateFamilyNameRequest("  Trimmed  "), admin.UserId!.Value, CT);

        result.Name.Should().Be("Trimmed");
    }

    // ── AddMemberAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddMemberAsync_AdminCaller_AddsMember()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.AddMemberAsync(
            new AddFamilyMemberRequest("Child", null, null, null, false),
            admin.UserId!.Value, CT);

        result.DisplayName.Should().Be("Child");
        result.IsLinked.Should().BeFalse();
        _db.FamilyMembers.Count().Should().Be(2);
    }

    [Fact]
    public async Task AddMemberAsync_NonAdmin_ThrowsForbidden()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        admin.IsAdmin = false;
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.AddMemberAsync(
            new AddFamilyMemberRequest("Child", null, null, null, false),
            admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateEmail_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        _db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = admin.FamilyId,
            DisplayName = "Existing",
            Email = "dupe@test.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.AddMemberAsync(
            new AddFamilyMemberRequest("New", "DUPE@test.com", null, null, false),
            admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*email*");
    }

    [Fact]
    public async Task AddMemberAsync_EmailMatchesExistingUser_AutoLinks()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = "ext1",
            Provider = Provider.Google,
            Email = "link@test.com",
            DisplayName = "Linked User",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(existingUser);
        await _db.SaveChangesAsync(CT);

        var result = await _sut.AddMemberAsync(
            new AddFamilyMemberRequest("Linked", "Link@Test.com", null, null, false),
            admin.UserId!.Value, CT);

        result.IsLinked.Should().BeTrue();
    }

    [Fact]
    public async Task AddMemberAsync_NoMember_ThrowsForbidden()
    {
        var act = () => _sut.AddMemberAsync(
            new AddFamilyMemberRequest("X", null, null, null, false),
            Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AddMemberAsync_NullEmail_NoLinkAttempt()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.AddMemberAsync(
            new AddFamilyMemberRequest("Child", null, new DateOnly(2015, 1, 1), null, false),
            admin.UserId!.Value, CT);

        result.IsLinked.Should().BeFalse();
        result.DateOfBirth.Should().Be(new DateOnly(2015, 1, 1));
    }

    [Fact]
    public async Task AddMemberAsync_EmailNoMatchingUser_NotLinked()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.AddMemberAsync(
            new AddFamilyMemberRequest("Someone", "nobody@test.com", null, null, false),
            admin.UserId!.Value, CT);

        result.IsLinked.Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_WithWeightOverride_SetsWeight()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.AddMemberAsync(
            new AddFamilyMemberRequest("Child", null, null, 0.5m, false),
            admin.UserId!.Value, CT);

        result.WeightOverride.Should().Be(0.5m);
    }

    [Fact]
    public async Task AddMemberAsync_IsAdminFlag_Preserved()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var result = await _sut.AddMemberAsync(
            new AddFamilyMemberRequest("CoAdmin", null, null, null, true),
            admin.UserId!.Value, CT);

        result.IsAdmin.Should().BeTrue();
    }

    // ── UpdateMemberAsync ───────────────────────────────────────────────────

    private FamilyMember SeedNonAdminMember(Guid familyId, string name = "Member", string? email = null)
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            DisplayName = name,
            Email = email,
            IsActive = true,
            IsAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.FamilyMembers.Add(member);
        _db.SaveChanges();
        return member;
    }

    [Fact]
    public async Task UpdateMemberAsync_AdminUpdatesOtherMember_Succeeds()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var member = SeedNonAdminMember(admin.FamilyId);

        var result = await _sut.UpdateMemberAsync(
            member.Id,
            new UpdateFamilyMemberRequest("Updated", "new@test.com", new DateOnly(2000, 1, 1), 0.5m, true),
            admin.UserId!.Value, CT);

        result.DisplayName.Should().Be("Updated");
        result.Email.Should().Be("new@test.com");
        result.DateOfBirth.Should().Be(new DateOnly(2000, 1, 1));
        result.WeightOverride.Should().Be(0.5m);
        result.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMemberAsync_MemberEditsSelf_Succeeds()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var userId = Guid.NewGuid();
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = admin.FamilyId,
            UserId = userId,
            DisplayName = "Self",
            IsActive = true,
            IsAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(CT);

        var result = await _sut.UpdateMemberAsync(
            member.Id,
            new UpdateFamilyMemberRequest("NewName", null, null, null, false),
            userId, CT);

        result.DisplayName.Should().Be("NewName");
    }

    [Fact]
    public async Task UpdateMemberAsync_NonAdminEditsOther_ThrowsForbidden()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var userId = Guid.NewGuid();
        var caller = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = admin.FamilyId,
            UserId = userId,
            DisplayName = "Caller",
            IsActive = true,
            IsAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.FamilyMembers.Add(caller);
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.UpdateMemberAsync(
            admin.Id,
            new UpdateFamilyMemberRequest("Hack", null, null, null, false),
            userId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateMemberAsync_MemberNotFound_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var act = () => _sut.UpdateMemberAsync(
            Guid.NewGuid(),
            new UpdateFamilyMemberRequest("X", null, null, null, false),
            admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateMemberAsync_DuplicateEmail_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var existing = SeedNonAdminMember(admin.FamilyId, "Existing", "taken@test.com");
        var target = SeedNonAdminMember(admin.FamilyId, "Target", "other@test.com");

        var act = () => _sut.UpdateMemberAsync(
            target.Id,
            new UpdateFamilyMemberRequest("Target", "TAKEN@test.com", null, null, false),
            admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*email*");
    }

    [Fact]
    public async Task UpdateMemberAsync_SameEmail_NoConflict()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var member = SeedNonAdminMember(admin.FamilyId, "Member", "keep@test.com");

        var result = await _sut.UpdateMemberAsync(
            member.Id,
            new UpdateFamilyMemberRequest("Member", "keep@test.com", null, null, false),
            admin.UserId!.Value, CT);

        result.Email.Should().Be("keep@test.com");
    }

    [Fact]
    public async Task UpdateMemberAsync_NonAdminCannotSelfElevate()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var userId = Guid.NewGuid();
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = admin.FamilyId,
            UserId = userId,
            DisplayName = "Regular",
            IsActive = true,
            IsAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(CT);

        var result = await _sut.UpdateMemberAsync(
            member.Id,
            new UpdateFamilyMemberRequest("Regular", null, null, null, true),
            userId, CT);

        result.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMemberAsync_NoCaller_ThrowsForbidden()
    {
        var act = () => _sut.UpdateMemberAsync(
            Guid.NewGuid(),
            new UpdateFamilyMemberRequest("X", null, null, null, false),
            Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateMemberAsync_InvalidRequest_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var act = () => _sut.UpdateMemberAsync(
            admin.Id,
            new UpdateFamilyMemberRequest("", null, null, null, false),
            admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateMemberAsync_NullEmail_SetsNull()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var member = SeedNonAdminMember(admin.FamilyId, "Member", "old@test.com");

        var result = await _sut.UpdateMemberAsync(
            member.Id,
            new UpdateFamilyMemberRequest("Member", null, null, null, false),
            admin.UserId!.Value, CT);

        result.Email.Should().BeNull();
    }

    // ── RemoveMemberAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMemberAsync_AdminRemovesMember_Deactivates()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var member = SeedNonAdminMember(admin.FamilyId);

        await _sut.RemoveMemberAsync(member.Id, admin.UserId!.Value, CT);

        var updated = await _db.FamilyMembers.FindAsync([member.Id], CT);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_NonAdmin_ThrowsForbidden()
    {
        var (_, admin) = SeedFamilyWithAdmin();
        var userId = Guid.NewGuid();
        var caller = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = admin.FamilyId,
            UserId = userId,
            DisplayName = "Regular",
            IsActive = true,
            IsAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.FamilyMembers.Add(caller);
        await _db.SaveChangesAsync(CT);

        var act = () => _sut.RemoveMemberAsync(admin.Id, userId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RemoveMemberAsync_AdminRemovesSelf_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var act = () => _sut.RemoveMemberAsync(admin.Id, admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cannot remove yourself*");
    }

    [Fact]
    public async Task RemoveMemberAsync_MemberNotFound_ThrowsValidation()
    {
        var (_, admin) = SeedFamilyWithAdmin();

        var act = () => _sut.RemoveMemberAsync(Guid.NewGuid(), admin.UserId!.Value, CT);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RemoveMemberAsync_NoCaller_ThrowsForbidden()
    {
        var act = () => _sut.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── ToDto ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToDto_MapsAllProperties()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            Email = "test@example.com",
            DateOfBirth = new DateOnly(1990, 5, 15),
            WeightOverride = 1.5m,
            IsActive = true,
            IsAdmin = true,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = FamilyService.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));

        result.Id.Should().Be(member.Id);
        result.DisplayName.Should().Be("Test");
        result.Email.Should().Be("test@example.com");
        result.DateOfBirth.Should().Be(new DateOnly(1990, 5, 15));
        result.WeightOverride.Should().Be(1.5m);
        result.IsActive.Should().BeTrue();
        result.IsAdmin.Should().BeTrue();
        result.IsLinked.Should().BeTrue();
        result.CreatedAt.Should().Be(member.CreatedAt);
    }

    [Fact]
    public void ToDto_NullUserId_IsLinkedFalse()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Unlinked",
            IsActive = true,
            UserId = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = FamilyService.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));

        result.IsLinked.Should().BeFalse();
    }

    [Fact]
    public void ToDto_NoWeightOverride_UsesCalculatedWeight()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Adult",
            DateOfBirth = new DateOnly(1990, 1, 1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var asOf = new DateOnly(2024, 6, 1);
        var result = FamilyService.ToDto(member, asOf);

        result.CurrentWeight.Should().Be(WeightCalculator.GetWeight(member, asOf));
        result.CurrentTier.Should().Be(WeightCalculator.GetTier(member, asOf));
    }
}
