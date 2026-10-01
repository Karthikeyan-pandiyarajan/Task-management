using System.Security.Claims;
using Application.Common;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Common;

public static class ClaimsPrincipalExtensions
{
    // The user id always comes from the validated token, never from the request body or route.
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedException("Token does not contain a valid user id.");

    public static string GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;
}
