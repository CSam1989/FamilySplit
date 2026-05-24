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
public record CreateGroupAction(CreateGroupRequest Request);
public record CreateGroupSuccessAction(GroupDetailDto Group);
public record CreateGroupFailureAction(string ErrorMessage);

// ── Update ────────────────────────────────────────────────────────────────────
public record UpdateGroupAction(Guid GroupId, UpdateGroupRequest Request);
public record UpdateGroupSuccessAction(GroupDetailDto Group);
public record UpdateGroupFailureAction(string ErrorMessage);

// ── Join ──────────────────────────────────────────────────────────────────────
public record JoinGroupAction(JoinGroupRequest Request);
public record JoinGroupSuccessAction(GroupDetailDto Group);
public record JoinGroupFailureAction(string ErrorMessage);

// ── Regenerate Invite Code ────────────────────────────────────────────────────
public record RegenerateInviteCodeAction(Guid GroupId);
public record RegenerateInviteCodeSuccessAction(Guid GroupId, string NewInviteCode);
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
