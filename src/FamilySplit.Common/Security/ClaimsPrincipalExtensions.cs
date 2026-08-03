using System.Security.Claims;

namespace FamilySplit.Common.Security;

/// <summary>Pulls the caller's UserId from the JWT (sub claim = User.Id Guid).</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("sub")
                  ?? throw new UnauthorizedAccessException("JWT missing sub/NameIdentifier claim.");
        return Guid.Parse(sub);
    }
}
