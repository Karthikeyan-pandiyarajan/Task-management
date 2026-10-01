using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthApiTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.AuthApi.CreateClient();

    [Fact]
    public async Task Ping_is_anonymous_and_returns_200()
    {
        var response = await _client.GetAsync("/api/auth/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_returns_201_with_token_and_no_password_data()
    {
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContainAny("Secret123", "passwordHash", "PBKDF2");
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.Email.Should().Be(email);
        auth.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_with_invalid_data_returns_400_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("not-an-email", "Secret123"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Contain("email");
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409()
    {
        var email = UniqueEmail();
        (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123"))).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email.ToUpperInvariant(), "Secret123"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_seeded_demo_user_returns_200_and_token()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo@taskapp.com", "Demo@1234"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("demo@taskapp.com", "Wrong1234")]
    [InlineData("nobody@taskapp.com", "Demo@1234")]
    public async Task Login_with_bad_credentials_returns_401_with_generic_message(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_token_returns_current_user()
    {
        var email = UniqueEmail();
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Secret123"));
        var token = (await register.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(email);
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@taskapp.com";
}
