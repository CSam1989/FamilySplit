using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Data;
using Moq;

namespace FamilySplit.UnitTests.Features.Expenses;

/// <summary>
/// Shared Moq scaffolding for the Expenses command-handler tests (ADR-001): the command handlers
/// are business logic over the <see cref="IExpenseData"/> seam and <see cref="IGroupMembershipGuard"/>,
/// so they are tested with mocks and <strong>no database</strong>. Data-access behaviour (the query
/// handlers and the <c>ExpenseData</c> gateway) is verified separately with Testcontainers in
/// <c>FamilySplit.IntegrationTests</c>.
/// </summary>
public abstract class ExpenseCommandTestBase
{
    protected readonly Mock<IExpenseData> Data = new();
    protected readonly Mock<IGroupMembershipGuard> Guard = new();

    protected static CancellationToken CT => TestContext.Current.CancellationToken;
    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    protected readonly Guid GroupId = Guid.NewGuid();
    protected readonly Guid ActivityId = Guid.NewGuid();
    protected readonly Guid CallerId = Guid.NewGuid();

    protected ExpenseCommandTestBase()
    {
        // Default the participant reads to empty so the reshuffle/seed branches don't NRE on the
        // Moq default. Tests that exercise participants override these with explicit setups.
        Data.Setup(d => d.GetActivityParticipantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Data.Setup(d => d.GetExpenseParticipantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    /// <summary>Arrange the activity read to return an activity in <see cref="GroupId"/> with the given status.</summary>
    protected void ArrangeActivity(ActivityStatus status = ActivityStatus.Open) =>
        Data.Setup(d => d.GetActivityAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityForExpense(GroupId, status));

    /// <summary>Arrange the expense read for Update/Delete (paid by <see cref="CallerId"/>, on <see cref="ActivityId"/>).</summary>
    protected void ArrangeExpense(Guid expenseId, ExpenseStatus status = ExpenseStatus.Active, string currency = "EUR") =>
        Data.Setup(d => d.GetExpenseAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseSnapshot(expenseId, ActivityId, CallerId, "Old", 50m, currency, Today, status));

    /// <summary>Arrange the ownership read so the caller passes the payer-family guard.</summary>
    protected void ArrangeCallerOwnsExpense()
    {
        var family = Guid.NewGuid();
        Data.Setup(d => d.GetExpenseOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseOwnership(IsGlobalAdmin: false, CallerFamilyId: family, PayerFamilyId: family));
    }
}
