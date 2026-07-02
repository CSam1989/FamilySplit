using FluentValidation;

namespace FamilySplit.Features.Notifications.Subscribe;

/// <summary>
/// Ported 1:1 from the legacy <c>PushNotificationService.ValidatePushField</c> — bounds the
/// attacker-supplied strings persisted to the database and requires <c>Endpoint</c> to be an
/// absolute https URL. Error messages match the legacy behaviour exactly so 422 responses are
/// unchanged.
/// </summary>
public sealed class SubscribeCommandValidator : AbstractValidator<SubscribeCommand>
{
    public SubscribeCommandValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .WithMessage("Endpoint is required and must be at most 2048 characters.")
            .MaximumLength(2048)
            .WithMessage("Endpoint is required and must be at most 2048 characters.");

        // Separate RuleFor so this check runs independently of the required/length rule above —
        // avoids FluentValidation's default "When applies to the whole chain" behaviour, which would
        // otherwise suppress the NotEmpty/MaximumLength errors for a blank or overlong endpoint.
        RuleFor(x => x.Endpoint)
            .Must(BeAnAbsoluteHttpsUrl)
            .WithMessage("Endpoint must be a valid https URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.Endpoint) && x.Endpoint.Length <= 2048);

        RuleFor(x => x.P256dh)
            .NotEmpty()
            .WithMessage("P256dh is required and must be at most 512 characters.")
            .MaximumLength(512)
            .WithMessage("P256dh is required and must be at most 512 characters.");

        RuleFor(x => x.Auth)
            .NotEmpty()
            .WithMessage("Auth is required and must be at most 512 characters.")
            .MaximumLength(512)
            .WithMessage("Auth is required and must be at most 512 characters.");
    }

    private static bool BeAnAbsoluteHttpsUrl(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
