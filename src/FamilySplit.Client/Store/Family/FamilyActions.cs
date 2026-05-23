using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Family;

// ── Load my family ────────────────────────────────────────────────────────────
public record LoadMyFamilyAction;
public record LoadMyFamilySuccessAction(FamilyDto Family);
public record LoadMyFamilyFailureAction(string ErrorMessage);

// ── Rename family (admin only) ────────────────────────────────────────────────
public record UpdateFamilyNameAction(UpdateFamilyNameRequest Request);
public record UpdateFamilyNameSuccessAction(FamilyDto Family);
public record UpdateFamilyNameFailureAction(string ErrorMessage);

// ── Add member (admin only) ───────────────────────────────────────────────────
public record AddFamilyMemberAction(AddFamilyMemberRequest Request);
public record AddFamilyMemberSuccessAction(FamilyMemberDto Member);
public record AddFamilyMemberFailureAction(string ErrorMessage);

// ── Update member (admin or self) ─────────────────────────────────────────────
public record UpdateFamilyMemberAction(Guid MemberId, UpdateFamilyMemberRequest Request);
public record UpdateFamilyMemberSuccessAction(FamilyMemberDto Member);
public record UpdateFamilyMemberFailureAction(string ErrorMessage);

// ── Remove member (admin only) ────────────────────────────────────────────────
public record RemoveFamilyMemberAction(Guid MemberId);
public record RemoveFamilyMemberSuccessAction(Guid MemberId);
public record RemoveFamilyMemberFailureAction(string ErrorMessage);

// ── Clear error ───────────────────────────────────────────────────────────────
public record ClearFamilyErrorAction;
