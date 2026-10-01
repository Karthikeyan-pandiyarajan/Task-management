using System.Security.Claims;
using Api.Auth.Controllers;
using Application.Abstractions;
using Application.Auth;
using Application.Common;
using Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

// Isolated controller tests: AuthService is real, its ports (repository, hasher, token generator) are mocked.
public class AuthControllerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenGenerator> _tokens = new();
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _tokens.Setup(t => t.Generate(It.IsAny<User>())).Returns("jwt");
        _sut = new AuthController(new AuthService(_users.Object, _hasher.Object, _tokens.Object));
    }

    [Fact]
    public async Task Register_returns_201_with_token_and_email_only()
    {
        var result = await _sut.Register(new RegisterRequest("new@taskapp.com", "Secret123"), default);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.ActionName.Should().Be(nameof(AuthController.Me));
        created.Value.Should().Be(new AuthResponse("jwt", "new@taskapp.com"));
    }

    [Fact]
    public async Task Register_lets_ConflictException_bubble_up_for_a_taken_email()
    {
        _users.Setup(u => u.GetByEmailAsync("taken@taskapp.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "taken@taskapp.com" });

        var act = () => _sut.Register(new RegisterRequest("taken@taskapp.com", "Secret123"), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_returns_the_token_for_valid_credentials()
    {
        _users.Setup(u => u.GetByEmailAsync("demo@taskapp.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "demo@taskapp.com", PasswordHash = "stored" });
        _hasher.Setup(h => h.Verify("Demo@1234", "stored")).Returns(true);

        var result = await _sut.Login(new LoginRequest("demo@taskapp.com", "Demo@1234"), default);

        result.Should().Be(new AuthResponse("jwt", "demo@taskapp.com"));
    }

    [Fact]
    public async Task Login_lets_UnauthorizedException_bubble_up_for_bad_credentials()
    {
        var act = () => _sut.Login(new LoginRequest("nobody@taskapp.com", "Whatever1"), default);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid credentials");
    }

    [Fact]
    public void Me_returns_id_and_email_from_the_token_claims()
    {
        var id = Guid.NewGuid();
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", id.ToString()), new Claim("email", "demo@taskapp.com")], "Test"))
            }
        };

        _sut.Me().Should().Be(new CurrentUserResponse(id, "demo@taskapp.com"));
    }

    [Fact]
    public void Ping_returns_200()
    {
        _sut.Ping().Should().BeOfType<OkObjectResult>();
    }
}
