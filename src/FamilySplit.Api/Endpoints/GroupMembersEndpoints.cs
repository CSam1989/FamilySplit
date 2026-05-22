// Group members are now managed at the Family level via /families/mine/members
// and /admin/families/{id}/members. This file is retained for git history.
// When a Family joins a Group, all active FamilyMembers participate automatically.
namespace FamilySplit.Api.Endpoints;

public static class GroupMembersEndpoints
{
    public static WebApplication MapGroupMemberEndpoints(this WebApplication app) => app;
}
