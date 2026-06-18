using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Activities;

// ── Load List ─────────────────────────────────────────────────────────────────
public record LoadActivitiesAction(Guid GroupId);
public record LoadActivitiesSuccessAction(List<ActivitySummaryDto> Activities);
public record LoadActivitiesFailureAction(string ErrorMessage);

// ── Load Detail ───────────────────────────────────────────────────────────────
public record LoadActivityDetailAction(Guid GroupId, Guid ActivityId);
public record LoadActivityDetailSuccessAction(ActivityDetailDto Activity);
public record LoadActivityDetailFailureAction(string ErrorMessage);

// ── Create top-level ──────────────────────────────────────────────────────────
// Strict CQRS: the create command returns only the new id; the effect re-queries the
// group-level list rather than patching state from a returned DTO.
public record CreateActivityAction(Guid GroupId, CreateActivityRequest Request);
public record CreateActivitySuccessAction(Guid ActivityId);
public record CreateActivityFailureAction(string ErrorMessage);

// ── Create sub-activity ───────────────────────────────────────────────────────
// Strict CQRS: the sub-create command returns only the new id; the effect re-queries the
// parent activity's detail so the new sub appears.
public record CreateSubActivityAction(Guid GroupId, Guid ParentActivityId, CreateActivityRequest Request);
public record CreateSubActivitySuccessAction(Guid ActivityId);
public record CreateSubActivityFailureAction(string ErrorMessage);

// ── Update ────────────────────────────────────────────────────────────────────
// Strict CQRS: the update command returns 204; the effect re-queries the detail.
public record UpdateActivityAction(Guid GroupId, Guid ActivityId, UpdateActivityRequest Request);
public record UpdateActivitySuccessAction(Guid ActivityId);
public record UpdateActivityFailureAction(string ErrorMessage);

// ── Close ─────────────────────────────────────────────────────────────────────
// Strict CQRS: the close command returns 204; the effect re-queries the detail + list.
public record CloseActivityAction(Guid GroupId, Guid ActivityId);
public record CloseActivitySuccessAction(Guid ActivityId);
public record CloseActivityFailureAction(string ErrorMessage);

// ── Add Participant ───────────────────────────────────────────────────────────
// Strict CQRS: the command returns 204; the effect re-queries the detail.
public record AddParticipantAction(Guid GroupId, Guid ActivityId, AddParticipantRequest Request);
public record AddParticipantSuccessAction(Guid ActivityId);
public record AddParticipantFailureAction(string ErrorMessage);

// ── Remove Participant ────────────────────────────────────────────────────────
// Strict CQRS: the command returns 204; the effect re-queries the detail.
public record RemoveParticipantAction(Guid GroupId, Guid ActivityId, Guid FamilyMemberId);
public record RemoveParticipantSuccessAction(Guid ActivityId);
public record RemoveParticipantFailureAction(string ErrorMessage);

// ── Clear Error ───────────────────────────────────────────────────────────────
public record ClearActivityErrorAction;
