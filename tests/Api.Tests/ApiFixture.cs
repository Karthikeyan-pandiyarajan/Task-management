using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Auth;
using Api.Tasks;
using Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Api.Tests;

// Hosts both real APIs in-memory, sharing one temporary SQLite file and one JWT configuration,
// exactly like the two processes share data/taskapp.db and the user-secret in development.
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"taskapp-tests-{Guid.NewGuid():N}.db");

    public WebApplicationFactory<AuthApiMarker> AuthApi { get; private set; } = null!;
    public WebApplicationFactory<TasksApiMarker> TasksApi { get; private set; } = null!;

    public Task InitializeAsync()
    {
        AuthApi = Configure(new WebApplicationFactory<AuthApiMarker>());
        TasksApi = Configure(new WebApplicationFactory<TasksApiMarker>());

        // Start sequentially so only one host runs migrations at a time.
        AuthApi.CreateClient().Dispose();
        TasksApi.CreateClient().Dispose();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await TasksApi.DisposeAsync();
        await AuthApi.DisposeAsync();
        SqliteConnection.ClearAllPools(); // release the file handle so it can be deleted
        File.Delete(_dbPath);
    }

    /// <summary>Registers a brand-new user through Api.Auth and returns a Tasks client carrying their token.</summary>
    public async Task<HttpClient> CreateAuthenticatedTasksClientAsync()
    {
        var token = await RegisterNewUserAsync();
        var client = TasksApi.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<string> RegisterNewUserAsync()
    {
        using var auth = AuthApi.CreateClient();
        var response = await auth.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@taskapp.com", "Secret123"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private WebApplicationFactory<T> Configure<T>(WebApplicationFactory<T> factory) where T : class
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing"); // no user-secrets, no OpenAPI
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = _dbPath,
                ["Jwt:Issuer"] = "TaskApp.Tests",
                ["Jwt:Audience"] = "TaskApp.Tests.Client",
                ["Jwt:Secret"] = "integration-test-secret-at-least-32-chars!!",
                ["Jwt:ExpiryMinutes"] = "5"
            }));
        });
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
