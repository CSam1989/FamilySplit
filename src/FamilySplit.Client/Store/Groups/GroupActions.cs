using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Groups;

// ── Load List ─────────────────────────────────────────────────────────────────
public record LoadGroupsAction;
public record LoadGroupsSuccessAction(List<GroupSummaryDto> Groups);
public record LoadGroupsFailureAction(string ErrorMessage);

// ── Load Detail ───────────────────────────────────────────────────────────────
public record LoadGroupDetailAction(Guid GroupId);
public record LoadGroupDetailSuccessAction(GroupDetailDto Group);
public record LoadGroupDetailFailureAction(string ErrorMessage);

// ── Create ────────────────────────────────────────────────────────────────────
// Strict CQRS: the create command returns only the new id; the effect navigates to
// the new group's detail page and re-queries the list rather than patching state.
public record CreateGroupAction(CreateGroupRequest Request);
public record CreateGroupSuccessAction(Guid GroupId);
public record CreateGroupFailureAction(string ErrorMessage);

// ── Update ────────────────────────────────────────────────────────────────────
// Strict CQRS: the update command returns 204; the effect re-queries the detail.
public record UpdateGroupAction(Guid GroupId, UpdateGroupRequest Request);
public record UpdateGroupSuccessAction(Guid GroupId);
public record UpdateGroupFailureAction(string ErrorMessage);

// ── Join ──────────────────────────────────────────────────────────────────────
// Documented exception: join returns 200 + the group id; the effect navigates to the
// new group's detail page and re-queries the list.
public record JoinGroupAction(JoinGroupRequest Request);
public record JoinGroupSuccessAction(Guid GroupId);
public record JoinGroupFailureAction(string ErrorMessage);

// ── Regenerate Invite Code ────────────────────────────────────────────────────
// Strict CQRS: regenerate returns 204; the effect re-queries the detail so the
// freshly-rotated invite code is picked up from GroupDetailDto.InviteCode.
public record RegenerateInviteCodeAction(Guid GroupId);
public record RegenerateInviteCodeSuccessAction(Guid GroupId);
public record RegenerateInviteCodeFailureAction(string ErrorMessage);

// ── Leave ─────────────────────────────────────────────────────────────────────
public record LeaveGroupAction(Guid GroupId);
public record LeaveGroupSuccessAction(Guid GroupId);
public record LeaveGroupFailureAction(string ErrorMessage);

// ── Delete (global-admin only) ────────────────────────────────────────────────
public record DeleteGroupAction(Guid GroupId);
public record DeleteGroupSuccessAction(Guid GroupId);
public record DeleteGroupFailureAction(string ErrorMessage);

// ── Admin: Add family to group ────────────────────────────────────────────────
public record AdminAddFamilyToGroupAction(Guid GroupId, Guid FamilyId);
public record AdminAddFamilyToGroupSuccessAction(Guid GroupId);
public record AdminAddFamilyToGroupFailureAction(string ErrorMessage);

// ── Admin: Remove family from group ──────────────────────────────────────────
public record AdminRemoveFamilyFromGroupAction(Guid GroupId, Guid FamilyId);
public record AdminRemoveFamilyFromGroupSuccessAction(Guid GroupId);
public record AdminRemoveFamilyFromGroupFailureAction(string ErrorMessage);

// ── Clear error ───────────────────────────────────────────────────────────────
public record ClearGroupErrorAction;
