using System.Text;
using Application.Abstractions;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenGenerator
{
    private readonly JsonWebTokenHandler _handler = new();

    public string Generate(User user)
    {
        var settings = options.Value;
        if (settings.Secret.Length < JwtOptions.MinSecretLength)
            throw new InvalidOperationException($"Jwt:Secret must be at least {JwtOptions.MinSecretLength} characters.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));

        // Only non-sensitive identifiers go in the token; it is signed, not encrypted.
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(settings.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
    }
}
