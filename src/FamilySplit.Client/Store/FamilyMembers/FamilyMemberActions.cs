using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.FamilyMembers;

// ── Load my profile ───────────────────────────────────────────────────────────
public record LoadMyProfileAction;
public record LoadMyProfileSuccessAction(FamilyMemberDto Profile);
public record LoadMyProfileFailureAction(string ErrorMessage);
