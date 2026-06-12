using FamilySplit.Common.Auditing;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilySplit.UnitTests.Features.Expenses;

/// <summary>
/// Shared in-memory <see cref="AppDbContext"/> + seed helpers for the Expenses slice
/// handler tests. Extracted from the old <c>ExpenseServiceTests</c> constructor.
/// </summary>
public abstract class ExpenseTestBase : IDisposable
{
    protected readonly AppDbContext Db;
    protected readonly AuditService Audit;
    protected readonly GroupMembershipGuard Guard;

    protected static CancellationToken CT => TestContext.Current.CancellationToken;

    // Shared test data.
    protected readonly Guid GroupId = Guid.NewGuid();
    protected readonly Guid ActivityId = Guid.NewGuid();
    protected readonly Guid CallerId = Guid.NewGuid();
    protected readonly Guid FamilyId = Guid.NewGuid();

    protected ExpenseTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new AppDbContext(options);
        Audit = new AuditService(Db, NullLogger<AuditService>.Instance);
        Guard = new GroupMembershipGuard(Db);
    }

    public void Dispose()
    {
        Db.Dispose();
        GC.SuppressFinalize(this);
    }

    protected async Task SeedGroupMembershipAsync()
    {
        Db.Families.Add(new Family { Id = FamilyId, Name = "TestFamily" });
        Db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = FamilyId,
            UserId = CallerId,
            DisplayName = "Caller",
            IsActive = true,
        });
        Db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = GroupId, FamilyId = FamilyId });
        await Db.SaveChangesAsync(CT);
    }

    protected async Task SeedActivityAsync(ActivityStatus status = ActivityStatus.Open)
    {
        Db.Activities.Add(new Activity
        {
            Id = ActivityId,
            GroupId = GroupId,
            Name = "TestActivity",
            Status = status,
            CreatedByUserId = CallerId,
        });
        await Db.SaveChangesAsync(CT);
    }

    /// <summary>
    /// Seeds a second family + member in the same group, plus an expense paid by the
    /// original caller. Returns (otherCallerId, expenseId) where otherCallerId is a
    /// member of a DIFFERENT family than the expense's payer. Used to exercise the
    /// payer-family ownership guard on the Update and Delete commands.
    /// </summary>
    protected async Task<(Guid OtherCallerId, Guid ExpenseId)> SeedExpenseByCallerWithOutsiderAsync(
        bool outsiderIsGlobalAdmin = false)
    {
        await SeedGroupMembershipAsync();
        await SeedActivityAsync();

        var otherFamilyId = Guid.NewGuid();
        var otherCallerId = Guid.NewGuid();
        Db.Families.Add(new Family { Id = otherFamilyId, Name = "OtherFamily" });
        Db.FamilyMembers.Add(new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = otherFamilyId,
            UserId = otherCallerId,
            DisplayName = "Outsider",
            IsActive = true,
        });
        Db.GroupFamilies.Add(new GroupFamily { Id = Guid.NewGuid(), GroupId = GroupId, FamilyId = otherFamilyId });
        Db.Users.Add(new User
        {
            Id = otherCallerId,
            Provider = Provider.Google,
            ExternalId = $"ext-{otherCallerId}",
            Email = "outsider@example.com",
            DisplayName = "Outsider",
            IsGlobalAdmin = outsiderIsGlobalAdmin,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var expenseId = Guid.NewGuid();
        Db.Expenses.Add(new Expense
        {
            Id = expenseId,
            ActivityId = ActivityId,
            PaidByUserId = CallerId, // paid by the ORIGINAL family
            Title = "Theirs",
            TotalAmount = 50,
            Currency = "EUR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await Db.SaveChangesAsync(CT);

        return (otherCallerId, expenseId);
    }
}
