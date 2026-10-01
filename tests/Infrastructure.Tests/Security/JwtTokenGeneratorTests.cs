using System.Text;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Tests.Security;

public class JwtTokenGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = "taskapp-test",
        Audience = "taskapp-test-clients",
        Secret = "test-secret-that-is-at-least-32-characters-long",
        ExpiryMinutes = 30
    };

    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "alice@taskapp.com", PasswordHash = "never-in-token" };

    private JwtTokenGenerator CreateSut() => new(Options.Create(_options), new FixedTimeProvider(Now));

    [Fact]
    public void Token_carries_user_id_email_issuer_audience_and_configured_expiry()
    {
        var jwt = new JsonWebToken(CreateSut().Generate(_user));

        jwt.Subject.Should().Be(_user.Id.ToString());
        jwt.GetClaim(JwtRegisteredClaimNames.Email).Value.Should().Be("alice@taskapp.com");
        jwt.Issuer.Should().Be("taskapp-test");
        jwt.Audiences.Should().ContainSingle("taskapp-test-clients");
        jwt.ValidTo.Should().Be(Now.UtcDateTime.AddMinutes(30));
        jwt.Claims.Should().NotContain(c => c.Value.Contains("never-in-token"));
    }

    [Fact]
    public async Task Token_signature_validates_with_the_configured_secret()
    {
        var token = CreateSut().Generate(_user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            ValidateLifetime = false // fixed clock is in the "past" relative to the machine clock
        });

        result.IsValid.Should().BeTrue(result.Exception?.Message);
    }

    [Fact]
    public void Secret_shorter_than_32_characters_is_rejected()
    {
        _options.Secret = "too-short";

        var act = () => CreateSut().Generate(_user);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32*");
    }
}
