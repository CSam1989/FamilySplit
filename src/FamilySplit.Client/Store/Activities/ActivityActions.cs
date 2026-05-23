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
public record CreateActivityAction(Guid GroupId, CreateActivityRequest Request);
public record CreateActivitySuccessAction(ActivityDetailDto Activity);
public record CreateActivityFailureAction(string ErrorMessage);

// ── Create sub-activity ───────────────────────────────────────────────────────
public record CreateSubActivityAction(Guid GroupId, Guid ParentActivityId, CreateActivityRequest Request);
public record CreateSubActivitySuccessAction(ActivityDetailDto Activity);
public record CreateSubActivityFailureAction(string ErrorMessage);

// ── Update ────────────────────────────────────────────────────────────────────
public record UpdateActivityAction(Guid GroupId, Guid ActivityId, UpdateActivityRequest Request);
public record UpdateActivitySuccessAction(ActivityDetailDto Activity);
public record UpdateActivityFailureAction(string ErrorMessage);

// ── Close ─────────────────────────────────────────────────────────────────────
public record CloseActivityAction(Guid GroupId, Guid ActivityId);
public record CloseActivitySuccessAction(ActivityDetailDto Activity);
public record CloseActivityFailureAction(string ErrorMessage);

// ── Add Participant ───────────────────────────────────────────────────────────
public record AddParticipantAction(Guid GroupId, Guid ActivityId, AddParticipantRequest Request);
public record AddParticipantSuccessAction(ActivityDetailDto Activity);
public record AddParticipantFailureAction(string ErrorMessage);

// ── Remove Participant ────────────────────────────────────────────────────────
public record RemoveParticipantAction(Guid GroupId, Guid ActivityId, Guid FamilyMemberId);
public record RemoveParticipantSuccessAction(ActivityDetailDto Activity);
public record RemoveParticipantFailureAction(string ErrorMessage);

// ── Clear Error ───────────────────────────────────────────────────────────────
public record ClearActivityErrorAction;
