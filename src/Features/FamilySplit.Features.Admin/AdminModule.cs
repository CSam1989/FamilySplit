using FamilySplit.Common.Modules;
using FamilySplit.Features.Admin.AddFamilyMember;
using FamilySplit.Features.Admin.AddFamilyToGroup;
using FamilySplit.Features.Admin.CreateFamily;
using FamilySplit.Features.Admin.Data;
using FamilySplit.Features.Admin.DeleteGroup;
using FamilySplit.Features.Admin.GetFamily;
using FamilySplit.Features.Admin.ListFamilies;
using FamilySplit.Features.Admin.RemoveFamilyFromGroup;
using FamilySplit.Features.Admin.RemoveFamilyMember;
using FamilySplit.Features.Admin.UpdateFamilyMember;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Admin;

/// <summary>
/// The Admin feature slice: global-admin family + member CRUD and group management. Every operation is
/// gated on <c>User.IsGlobalAdmin</c> (the query side via <c>AdminGate</c>, the command side via the
/// mockable <c>IAdminData.IsGlobalAdminAsync</c> seam). Commands use the strict-CQRS response shapes
/// (201 + id on create / add-member, 204 on the rest); the client re-queries after mutating.
/// </summary>
public sealed class AdminModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(AdminModule).Assembly);

        services.AddScoped<IAdminData, AdminData>();      // the data seam (ADR-001)

        services.AddScoped<ListFamiliesQueryHandler>();
        services.AddScoped<GetFamilyQueryHandler>();
        services.AddScoped<CreateFamilyCommandHandler>();
        services.AddScoped<AddFamilyMemberCommandHandler>();
        services.AddScoped<UpdateFamilyMemberCommandHandler>();
        services.AddScoped<RemoveFamilyMemberCommandHandler>();
        services.AddScoped<DeleteGroupCommandHandler>();
        services.AddScoped<AddFamilyToGroupCommandHandler>();
        services.AddScoped<RemoveFamilyFromGroupCommandHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/admin")
            .WithTags("Admin");

        ListFamiliesEndpoint.Map(grp);
        CreateFamilyEndpoint.Map(grp);
        GetFamilyEndpoint.Map(grp);
        AddFamilyMemberEndpoint.Map(grp);
        UpdateFamilyMemberEndpoint.Map(grp);
        RemoveFamilyMemberEndpoint.Map(grp);
        DeleteGroupEndpoint.Map(grp);
        AddFamilyToGroupEndpoint.Map(grp);
        RemoveFamilyFromGroupEndpoint.Map(grp);
    }
}
