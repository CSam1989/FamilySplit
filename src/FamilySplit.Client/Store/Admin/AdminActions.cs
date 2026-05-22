using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Admin;

// ── Load all families ─────────────────────────────────────────────────────────
public record LoadAdminFamiliesAction;
public record LoadAdminFamiliesSuccessAction(List<FamilyDto> Families);
public record LoadAdminFamiliesFailureAction(string ErrorMessage);

// ── Load one family ───────────────────────────────────────────────────────────
public record LoadAdminFamilyAction(Guid FamilyId);
public record LoadAdminFamilySuccessAction(FamilyDto Family);
public record LoadAdminFamilyFailureAction(string ErrorMessage);

// ── Create family ─────────────────────────────────────────────────────────────
public record CreateAdminFamilyAction(CreateFamilyRequest Request);
public record CreateAdminFamilySuccessAction(FamilyDto Family);
public record CreateAdminFamilyFailureAction(string ErrorMessage);

// ── Add member ────────────────────────────────────────────────────────────────
public record AddAdminMemberAction(Guid FamilyId, AddFamilyMemberRequest Request);
public record AddAdminMemberSuccessAction(Guid FamilyId);
public record AddAdminMemberFailureAction(string ErrorMessage);

// ── Update member ─────────────────────────────────────────────────────────────
public record UpdateAdminMemberAction(Guid FamilyId, Guid MemberId, UpdateFamilyMemberRequest Request);
public record UpdateAdminMemberSuccessAction(FamilyMemberDto Member);
public record UpdateAdminMemberFailureAction(string ErrorMessage);

// ── Remove member ─────────────────────────────────────────────────────────────
public record RemoveAdminMemberAction(Guid FamilyId, Guid MemberId);
public record RemoveAdminMemberSuccessAction(Guid FamilyId, Guid MemberId);
public record RemoveAdminMemberFailureAction(string ErrorMessage);
