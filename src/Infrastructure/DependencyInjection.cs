using Application.Abstractions;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Options are bound lazily (at first resolve), so test hosts can override configuration.
        // ValidateOnStart makes a missing JWT secret or DB path fail at startup, not on the first request.
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var path = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Path;
            var contentRoot = sp.GetRequiredService<IHostEnvironment>().ContentRootPath;
            var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(contentRoot, path));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            options.UseSqlite($"Data Source={fullPath}");
        });

        services.AddScoped<ITaskRepository, EfTaskRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<DemoDataSeeder>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        return services;
    }

    /// <summary>Applies pending migrations and seeds demo data. Safe to call from both APIs.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        try
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The other API seeded at the same moment and won the unique-email race; the data is there.
        }
    }
}
