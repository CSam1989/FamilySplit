using FamilySplit.Common.Modules;
using FamilySplit.Features.Families.AddMember;
using FamilySplit.Features.Families.Data;
using FamilySplit.Features.Families.GetMyFamily;
using FamilySplit.Features.Families.GetMyProfile;
using FamilySplit.Features.Families.RemoveMember;
using FamilySplit.Features.Families.UpdateFamilyName;
using FamilySplit.Features.Families.UpdateMember;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Families;

/// <summary>
/// The Families feature slice: management of the caller's own Family. Family admins
/// (<c>FamilyMember.IsAdmin</c>) can add/update/remove other members and rename the family; any member
/// can read their own family and update their own profile. Also absorbs the legacy
/// <c>GET /users/me/profile</c> endpoint (<see cref="GetMyProfileQueryHandler"/>). Commands use the
/// strict-CQRS response shapes (201 + id on add-member, 204 on the rest); the client re-queries after
/// mutating.
/// </summary>
public sealed class FamiliesModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(FamiliesModule).Assembly);

        services.AddScoped<IFamilyData, FamilyData>();    // the data seam (ADR-001)

        services.AddScoped<GetMyFamilyQueryHandler>();
        services.AddScoped<GetMyProfileQueryHandler>();
        services.AddScoped<UpdateFamilyNameCommandHandler>();
        services.AddScoped<AddMemberCommandHandler>();
        services.AddScoped<UpdateMemberCommandHandler>();
        services.AddScoped<RemoveMemberCommandHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var familyGrp = endpoints
            .MapGroup("/families/mine")
            .WithTags("Family");

        GetMyFamilyEndpoint.Map(familyGrp);
        UpdateFamilyNameEndpoint.Map(familyGrp);
        AddMemberEndpoint.Map(familyGrp);
        UpdateMemberEndpoint.Map(familyGrp);
        RemoveMemberEndpoint.Map(familyGrp);

        var profileGrp = endpoints
            .MapGroup("/users/me")
            .WithTags("Profile");

        GetMyProfileEndpoint.Map(profileGrp);
    }
}
