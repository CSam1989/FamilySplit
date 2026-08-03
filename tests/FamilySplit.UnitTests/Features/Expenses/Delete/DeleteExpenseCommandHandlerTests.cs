using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Data;
using FamilySplit.Features.Expenses.Delete;
using FamilySplit.UnitTests.Features.Expenses;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Expenses.Delete;

public class DeleteExpenseCommandHandlerTests : ExpenseCommandTestBase
{
    private readonly DeleteExpenseCommandHandler _sut;
    private AuditEntry? _audit;

    public DeleteExpenseCommandHandlerTests()
    {
        _sut = new DeleteExpenseCommandHandler(
            Data.Object, Guard.Object, NullLogger<DeleteExpenseCommandHandler>.Instance);

        Data.Setup(d => d.DeleteExpenseAsync(It.IsAny<Guid>(), It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, AuditEntry, CancellationToken>((_, a, _) => _audit = a)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_ExpenseNotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(Guid.NewGuid(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Expense not found.*");
    }

    [Fact]
    public async Task Handle_ActivityNotFound_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        // GetActivityAsync unconfigured → null.

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Activity not found.*");
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        Guard.Setup(g => g.RequireGroupMemberAsync(GroupId, CallerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException());

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_SettledActivity_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity(ActivityStatus.Settled);
        ArrangeCallerOwnsExpense();

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task Handle_LockedExpense_ThrowsValidationException()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId, ExpenseStatus.Locked);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task Handle_CallerFromDifferentFamily_ThrowsForbidden()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        Data.Setup(d => d.GetExpenseOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseOwnership(IsGlobalAdmin: false, CallerFamilyId: Guid.NewGuid(), PayerFamilyId: Guid.NewGuid()));

        Func<Task> act = () => _sut.HandleAsync(expenseId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Valid_RemovesExpense()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        await _sut.HandleAsync(expenseId, CallerId, CT);

        Data.Verify(d => d.DeleteExpenseAsync(expenseId, It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Valid_BuildsDeletedAuditEntry()
    {
        var expenseId = Guid.NewGuid();
        ArrangeExpense(expenseId);
        ArrangeActivity();
        ArrangeCallerOwnsExpense();

        await _sut.HandleAsync(expenseId, CallerId, CT);

        _audit.Should().NotBeNull();
        _audit!.EntityType.Should().Be("Expense");
        _audit.Action.Should().Be("Deleted");
        _audit.EntityId.Should().Be(expenseId);
    }
}
