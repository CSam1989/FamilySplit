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
// Strict CQRS: the command returns 201 + id; success carries no payload — the effect re-queries the list.
public record CreateAdminFamilyAction(CreateFamilyRequest Request);
public record CreateAdminFamilySuccessAction;
public record CreateAdminFamilyFailureAction(string ErrorMessage);

// ── Add member ────────────────────────────────────────────────────────────────
public record AddAdminMemberAction(Guid FamilyId, AddFamilyMemberRequest Request);
public record AddAdminMemberSuccessAction(Guid FamilyId);
public record AddAdminMemberFailureAction(string ErrorMessage);

// ── Update member ─────────────────────────────────────────────────────────────
// Strict CQRS: the command returns 204; success carries the family id — the effect re-queries it.
public record UpdateAdminMemberAction(Guid FamilyId, Guid MemberId, UpdateFamilyMemberRequest Request);
public record UpdateAdminMemberSuccessAction(Guid FamilyId);
public record UpdateAdminMemberFailureAction(string ErrorMessage);

// ── Remove member ─────────────────────────────────────────────────────────────
public record RemoveAdminMemberAction(Guid FamilyId, Guid MemberId);
public record RemoveAdminMemberSuccessAction(Guid FamilyId, Guid MemberId);
public record RemoveAdminMemberFailureAction(string ErrorMessage);

// ── Clear error ───────────────────────────────────────────────────────────────
public record ClearAdminErrorAction;
