using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FamilySplit.IntegrationTests.Infrastructure;

/// <summary>
/// Mints short-lived JWTs for integration test clients without requiring
/// the full DI stack. Mirrors the claim structure in JwtFactory exactly
/// so the API's ClaimsPrincipalExtensions.GetUserId() resolves correctly.
/// </summary>
public static class JwtHelper
{
    private const string TestIssuer = "familysplit";
    private const string TestAudience = "familysplit-client";

    /// <summary>
    /// Mint a JWT signed with <paramref name="signingKey"/>.
    /// </summary>
    /// <param name="userId">The User.Id — becomes the <c>sub</c> claim.</param>
    /// <param name="email">The user's email — becomes the <c>email</c> claim.</param>
    /// <param name="displayName">The user's display name — becomes the <c>name</c> claim.</param>
    /// <param name="isGlobalAdmin">Ignored for JWT claims (the flag lives only in the DB), but
    ///     kept as a parameter so callers document intent at the call site.</param>
    /// <param name="signingKey">Must match <c>Jwt:SigningKey</c> in the test host config.</param>
    /// <param name="lifetimeMinutes">Token lifetime; defaults to 60 minutes for tests.</param>
    public static string Mint(
        Guid userId,
        string email,
        string displayName,
        bool isGlobalAdmin,
        string signingKey,
        int lifetimeMinutes = 60)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", displayName),
            new Claim("provider", "Google"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
