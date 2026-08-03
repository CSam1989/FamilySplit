using FamilySplit.Common.Modules;
using FamilySplit.Features.Groups.Create;
using FamilySplit.Features.Groups.Data;
using FamilySplit.Features.Groups.GetDetail;
using FamilySplit.Features.Groups.Join;
using FamilySplit.Features.Groups.Leave;
using FamilySplit.Features.Groups.List;
using FamilySplit.Features.Groups.RegenerateInviteCode;
using FamilySplit.Features.Groups.Update;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Groups;

/// <summary>
/// The Groups feature slice: group CRUD, invite-code join/regeneration, and leave. Queries are pure
/// data access; commands hold the business logic and use the strict-CQRS response shapes (201 + id
/// on create, 204 on update/regenerate/leave, 200 + id on join — the documented exception).
/// </summary>
public sealed class GroupsModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(GroupsModule).Assembly);

        services.AddScoped<IGroupData, GroupData>();      // the data seam (ADR-001)
        services.AddScoped<ListGroupsQueryHandler>();
        services.AddScoped<GetGroupDetailQueryHandler>();
        services.AddScoped<CreateGroupCommandHandler>();
        services.AddScoped<UpdateGroupCommandHandler>();
        services.AddScoped<JoinGroupCommandHandler>();
        services.AddScoped<LeaveGroupCommandHandler>();
        services.AddScoped<RegenerateInviteCodeCommandHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/groups")
            .WithTags("Groups");

        ListGroupsEndpoint.Map(grp);
        CreateGroupEndpoint.Map(grp);
        JoinGroupEndpoint.Map(grp);
        GetGroupDetailEndpoint.Map(grp);
        UpdateGroupEndpoint.Map(grp);
        RegenerateInviteCodeEndpoint.Map(grp);
        LeaveGroupEndpoint.Map(grp);
    }
}
