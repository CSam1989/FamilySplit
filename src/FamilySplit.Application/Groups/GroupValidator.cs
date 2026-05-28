using FamilySplit.Application.Groups.Dtos;
using FluentValidation;

namespace FamilySplit.Application.Groups;

public class CreateGroupValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MaximumLength(100).WithMessage("Group name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}

public class UpdateGroupValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MaximumLength(100).WithMessage("Group name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}

public class JoinGroupValidator : AbstractValidator<JoinGroupRequest>
{
    public JoinGroupValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty().WithMessage("Invite code is required.")
            .Length(8).WithMessage("Invite code must be exactly 8 characters.");
    }
}
