using System.Security.Claims;

namespace PassItOn.Api.Auth;

public static class ClaimsExtensions
{
    /// <summary>
    /// Returns the authenticated user's ID from the JWT sub claim,
    /// or null if the claim is absent or unparseable.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue("sub");

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
