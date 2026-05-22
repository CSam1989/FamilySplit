using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FamilySplit.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace FamilySplit.Api.Auth;

/// <summary>
/// Issues signed JWTs after a successful OAuth callback.
/// Configuration keys (Jwt:SigningKey, Jwt:Issuer, Jwt:Audience, Jwt:LifetimeMinutes)
/// are read from IConfiguration via user-secrets / env vars.
/// </summary>
public class JwtFactory
{
    private readonly IConfiguration _config;

    public JwtFactory(IConfiguration config) => _config = config;

    public string Create(User user)
    {
        var jwt = _config.GetSection("Jwt");
        var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Missing Jwt:SigningKey.");
        var issuer = jwt["Issuer"] ?? "familysplit";
        var audience = jwt["Audience"] ?? "familysplit-client";
        var lifetimeMinutes = int.TryParse(jwt["LifetimeMinutes"], out var lm) ? lm : 60;

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
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
