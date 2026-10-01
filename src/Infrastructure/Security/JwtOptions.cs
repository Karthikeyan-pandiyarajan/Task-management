using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Security;

// Bound from the "Jwt" configuration section. The secret must come from user-secrets or the Jwt__Secret
// environment variable; appsettings.json intentionally leaves it empty.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinSecretLength = 32; // HS256 needs a key of at least 256 bits.

    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Secret is not configured. Run: dotnet user-secrets set \"Jwt:Secret\" \"<32+ chars>\" --project src/Api.Auth")]
    [MinLength(MinSecretLength)]
    public string Secret { get; set; } = string.Empty;

    [Range(1, 24 * 60)] public int ExpiryMinutes { get; set; } = 60;
}
