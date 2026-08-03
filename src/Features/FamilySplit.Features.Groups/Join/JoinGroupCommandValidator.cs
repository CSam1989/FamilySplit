using FluentValidation;

namespace FamilySplit.Features.Groups.Join;

public sealed class JoinGroupCommandValidator : AbstractValidator<JoinGroupCommand>
{
    public JoinGroupCommandValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty().WithMessage("Invite code is required.")
            .Length(8).WithMessage("Invite code must be exactly 8 characters.");
    }
}
