using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Create;
using FamilySplit.Features.Expenses.Data;
using FamilySplit.UnitTests.Features.Expenses;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Expenses.Create;

public class CreateExpenseCommandHandlerTests : ExpenseCommandTestBase
{
    private readonly CreateExpenseCommandHandler _sut;

    // Captured arguments from the data gateway's persist call.
    private Expense? _added;
    private IReadOnlyList<ExpenseParticipant>? _addedParticipants;
    private AuditEntry? _audit;

    public CreateExpenseCommandHandlerTests()
    {
        _sut = new CreateExpenseCommandHandler(
            Data.Object, new CreateExpenseCommandValidator(), Guard.Object,
            NullLogger<CreateExpenseCommandHandler>.Instance);

        Data.Setup(d => d.AddExpenseAsync(
                It.IsAny<Expense>(), It.IsAny<IReadOnlyList<ExpenseParticipant>>(), It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Expense, IReadOnlyList<ExpenseParticipant>, AuditEntry, CancellationToken>(
                (e, p, a, _) => { _added = e; _addedParticipants = p; _audit = a; })
            .Returns(Task.CompletedTask);
    }

    private static CreateExpenseCommand MakeCommand(decimal amount = 100m, string title = "Dinner")
        => new(title, "desc", amount, "EUR", Today, null);

    [Fact]
    public async Task Handle_ActivityNotFound_ThrowsValidationException()
    {
        // GetActivityAsync is unconfigured → returns null.
        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_SettledActivity_ThrowsValidationException()
    {
        ArrangeActivity(ActivityStatus.Settled);

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*settled*");
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeActivity();
        Guard.Setup(g => g.RequireGroupMemberAsync(GroupId, CallerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException());

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Valid_CreatesExpenseAndReturnsId()
    {
        ArrangeActivity();
        Data.Setup(d => d.GetActivityParticipantsAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var id = await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        id.Should().NotBeEmpty();
        _added.Should().NotBeNull();
        id.Should().Be(_added!.Id);
        _added.Title.Should().Be("Dinner");
        _added.TotalAmount.Should().Be(100);
        _added.Currency.Should().Be("EUR");
        Data.Verify(d => d.AddExpenseAsync(
            It.IsAny<Expense>(), It.IsAny<IReadOnlyList<ExpenseParticipant>>(), It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullCurrency_DefaultsToEUR()
    {
        ArrangeActivity();
        Data.Setup(d => d.GetActivityParticipantsAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cmd = new CreateExpenseCommand("Test", null, 50, null, Today, null);
        await _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        _added!.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Handle_TitleWithWhitespace_GetsTrimmed()
    {
        ArrangeActivity();
        Data.Setup(d => d.GetActivityParticipantsAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cmd = new CreateExpenseCommand("  Trimmed  ", null, 50, "EUR", Today, null);
        await _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        _added!.Title.Should().Be("Trimmed");
    }

    [Fact]
    public async Task Handle_WithActivityParticipants_SeedsExpenseParticipants()
    {
        ArrangeActivity();
        var memberId = Guid.NewGuid();
        Data.Setup(d => d.GetActivityParticipantsAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ParticipantSnapshotInput(memberId, new DateOnly(2000, 1, 1), null)]);

        await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        _addedParticipants.Should().ContainSingle()
            .Which.FamilyMemberId.Should().Be(memberId);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        // Validation runs first — no data setup needed.
        var cmd = new CreateExpenseCommand("", null, 0, "EUR", Today, null);
        Func<Task> act = () => _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_CurrencyDiffersFromExistingExpense_ThrowsValidation()
    {
        ArrangeActivity();
        Data.Setup(d => d.GetActivityCurrencyAsync(ActivityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("EUR");

        var cmd = new CreateExpenseCommand("Second", null, 20, "USD", Today, null);
        Func<Task> act = () => _sut.HandleAsync(ActivityId, cmd, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*same currency*");
        Data.Verify(d => d.AddExpenseAsync(
            It.IsAny<Expense>(), It.IsAny<IReadOnlyList<ExpenseParticipant>>(), It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Valid_BuildsCreatedAuditEntry()
    {
        ArrangeActivity();
        Data.Setup(d => d.GetActivityParticipantsAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var id = await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        _audit.Should().NotBeNull();
        _audit!.EntityType.Should().Be("Expense");
        _audit.Action.Should().Be("Created");
        _audit.EntityId.Should().Be(id);
        _audit.UserId.Should().Be(CallerId);
    }
}
