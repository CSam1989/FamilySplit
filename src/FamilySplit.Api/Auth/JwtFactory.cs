using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FamilySplit.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace FamilySplit.Api.Auth;

/// <summary>
/// Issues short-lived signed JWTs. Long-lived session persistence is handled
/// separately by <see cref="FamilySplit.Application.Auth.RefreshTokenService"/>
/// — the JWT itself never lives longer than <c>Jwt:LifetimeMinutes</c>
/// (default 15 minutes).
/// </summary>
public class JwtFactory
{
    private readonly IConfiguration _config;

    public JwtFactory(IConfiguration config) => _config = config;

    /// <summary>JWT lifetime in minutes. Exposed for the client so it can pre-empt expiry.</summary>
    public int LifetimeMinutes =>
        int.TryParse(_config["Jwt:LifetimeMinutes"], out var lm) ? lm : 15;

    public string Create(User user)
    {
        var jwt = _config.GetSection("Jwt");
        var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Missing Jwt:SigningKey.");
        var issuer = jwt["Issuer"] ?? "familysplit";
        var audience = jwt["Audience"] ?? "familysplit-client";

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.DisplayName),
            new Claim("provider", user.Provider.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(LifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
