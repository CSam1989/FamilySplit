using FluentValidation;

namespace FamilySplit.Features.Activities.AddParticipant;

public sealed class AddParticipantCommandValidator : AbstractValidator<AddParticipantCommand>
{
    public AddParticipantCommandValidator()
    {
        RuleFor(x => x.FamilyMemberId)
            .NotEmpty().WithMessage("FamilyMemberId is required.");
    }
}
