using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Data;
using FamilySplit.Features.Expenses.Update;
using FamilySplit.UnitTests.Features.Expenses;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Expenses.Update;

public class UpdateExpenseCommandHandlerTests : ExpenseCommandTestBase
{
    private readonly UpdateExpenseCommandHandler _sut;

    // Captured arguments from the data gateway's persist call.
    private ExpenseFields? _fields;
    private IReadOnlyList<ParticipantShare>? _recomputed;
    private AuditEntry? _audit;

    public UpdateExpenseCommandHandlerTests()
    {
        _sut = new UpdateExpenseCommandHandler(
            Data.Object, new UpdateExpenseCommandValidator(), Guard.Object,
            NullLogger<UpdateExpenseCommandHandler>.Instance);

        Data.Setup(d => d.UpdateExpenseAsync(
                It.IsAny<Guid>(), It.IsAny<ExpenseFields>(), It.IsAny<IReadOnlyList<ParticipantShare>?>(), It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ExpenseFields, IReadOnlyList<ParticipantShare>?, AuditEntry, CancellationToken>(
                (_, f, r, a, _) => { _fields = f; _recomputed = r; _audit = a; })
            .Returns(Task.CompletedTask);
    }

    private static UpdateExpenseCommand MakeCommand(
        string title = "New Title", decimal amount = 200m, string? currency = "EUR", DateOnly? date = null)
        => new(title, "new desc", amount, currency, date ?? Today, null);

    [Fact]
    public async Task Handle_ExpenseNotFound_ThrowsValidationException()
    {
        // GetExpenseAsync unconfigured → null.
        Func<Task> act = () => _sut.HandleAsync(Guid.NewGuid(), MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Expense not found.*");
    }

    [Fact]
    public async Task Handle_SettledActivity_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity(ActivityStatus.Settled);
        ArrangeCallerOwnsExpense();

        Func<Task> act = () => _sut.HandleAsync(expenseId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task Handle_LockedExpense_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId, ExpenseStatus.Locked);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        Func<Task> act = () => _sut.HandleAsync(expenseId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task Handle_Valid_UpdatesExpense()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        var cmd = new UpdateExpenseCommand("New Title", "new desc", 200, "USD", Today, null);
        await _sut.HandleAsync(expenseId, cmd, CallerId, CT);

        _fields.Should().NotBeNull();
        _fields!.Title.Should().Be("New Title");
        _fields.TotalAmount.Should().Be(200);
        _fields.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_AmountChanged_RecalculatesShares()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);           // existing amount 50
        ArrangeActivity();
        ArrangeCallerOwnsExpense();
        var memberId = Guid.NewGuid();
        Data.Setup(d => d.GetExpenseParticipantsAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ParticipantReshuffleInput(Guid.NewGuid(), memberId, new DateOnly(2000, 1, 1), null, false)]);

        await _sut.HandleAsync(expenseId, MakeCommand(amount: 200m), CallerId, CT);

        _recomputed.Should().NotBeNull();
        _recomputed.Should().ContainSingle().Which.CalculatedAmount.Should().Be(200);
    }

    [Fact]
    public async Task Handle_SameAmountAndDate_DoesNotRecalculate()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);           // existing amount 50, date Today
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        await _sut.HandleAsync(expenseId, MakeCommand(amount: 50m, date: Today), CallerId, CT);

        _recomputed.Should().BeNull("amount and date are unchanged");
        Data.Verify(d => d.GetExpenseParticipantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullCurrency_KeepsExistingCurrency()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId, currency: "GBP");
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        await _sut.HandleAsync(expenseId, MakeCommand(amount: 50m, currency: null, date: Today), CallerId, CT);

        _fields!.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        Guard.Setup(g => g.RequireGroupMemberAsync(GroupId, CallerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException());

        Func<Task> act = () => _sut.HandleAsync(expenseId, MakeCommand(amount: 1m), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_DateChanged_RecalculatesShares()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();
        var memberId = Guid.NewGuid();
        Data.Setup(d => d.GetExpenseParticipantsAsync(expenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ParticipantReshuffleInput(Guid.NewGuid(), memberId, new DateOnly(2000, 1, 1), null, false)]);

        // Same amount (50) but a different date → reshuffle. One participant → keeps full 50.
        await _sut.HandleAsync(expenseId, MakeCommand(amount: 50m, date: new DateOnly(2024, 6, 1)), CallerId, CT);

        _recomputed.Should().ContainSingle().Which.CalculatedAmount.Should().Be(50);
    }

    [Fact]
    public async Task Handle_CallerFromDifferentFamily_ThrowsForbidden()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        Data.Setup(d => d.GetExpenseOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseOwnership(IsGlobalAdmin: false, CallerFamilyId: Guid.NewGuid(), PayerFamilyId: Guid.NewGuid()));

        Func<Task> act = () => _sut.HandleAsync(expenseId, MakeCommand(title: "Hacked", amount: 999m), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_GlobalAdminFromDifferentFamily_Succeeds()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        Data.Setup(d => d.GetExpenseOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseOwnership(IsGlobalAdmin: true, CallerFamilyId: Guid.NewGuid(), PayerFamilyId: Guid.NewGuid()));

        await _sut.HandleAsync(expenseId, MakeCommand(title: "Admin edit", amount: 60m), CallerId, CT);

        _fields!.Title.Should().Be("Admin edit");
        _fields.TotalAmount.Should().Be(60);
    }

    [Fact]
    public async Task Handle_Valid_BuildsUpdatedAuditEntry()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        await _sut.HandleAsync(expenseId, MakeCommand(amount: 50m, date: Today), CallerId, CT);

        _audit!.EntityType.Should().Be("Expense");
        _audit.Action.Should().Be("Updated");
        _audit.EntityId.Should().Be(expenseId);
    }
}
