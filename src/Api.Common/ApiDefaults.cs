using System.Text;
using System.Text.Json.Serialization;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Common;

// Cross-cutting HTTP setup shared by Api.Auth and Api.Tasks so both behave identically.
public static class ApiDefaults
{
    public const string ClientCorsPolicy = "Client";

    public static IServiceCollection AddApiDefaults(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        // Configured lazily from JwtOptions so both APIs validate exactly what JwtTokenGenerator issues.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false; // keep "sub"/"email" as-is instead of long XML claim URIs
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Secret)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Email,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        // Secure by default: every endpoint requires a valid token unless it opts out with [AllowAnonymous].
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((cors, config) =>
        {
            var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            cors.AddPolicy(ClientCorsPolicy, p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
        });

        return services;
    }

    public static WebApplication UseApiDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(); // ProblemDetails body for bare 401/404 responses too
        app.UseCors(ClientCorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }
}
