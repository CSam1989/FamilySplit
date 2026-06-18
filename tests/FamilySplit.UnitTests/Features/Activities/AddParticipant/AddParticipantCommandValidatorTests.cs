using FamilySplit.Features.Activities.AddParticipant;
using FluentValidation.TestHelper;

namespace FamilySplit.UnitTests.Features.Activities.AddParticipant;

public class AddParticipantCommandValidatorTests
{
    private readonly AddParticipantCommandValidator _sut = new();

    [Fact]
    public void EmptyFamilyMemberId_HasValidationError()
    {
        var result = _sut.TestValidate(new AddParticipantCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.FamilyMemberId)
              .WithErrorMessage("FamilyMemberId is required.");
    }

    [Fact]
    public void ValidFamilyMemberId_HasNoValidationErrors()
    {
        var result = _sut.TestValidate(new AddParticipantCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
