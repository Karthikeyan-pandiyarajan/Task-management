using Application.Abstractions;
using Application.Auth;
using Application.Common;
using Domain.Entities;
using FluentAssertions;
using Moq;

namespace Application.Tests.Auth;

public class AuthServiceTests
{
    private const string ValidPassword = "Secret123";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenGenerator> _tokens = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => $"HASHED::{p.Length}");
        _sut = new AuthService(_users.Object, _hasher.Object, _tokens.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plainaddress")]
    [InlineData("missing-domain@")]
    [InlineData("no-tld@example")]
    [InlineData("has space@example.com")]
    public async Task Register_with_invalid_email_throws(string? email)
    {
        var act = () => _sut.RegisterAsync(new RegisterRequest(email!, ValidPassword));

        await act.Should().ThrowAsync<AppValidationException>().WithMessage("*email*");
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ab1")]        // too short
    [InlineData("abcdefgh")]   // no digit
    [InlineData("12345678")]   // no letter
    public async Task Register_with_weak_password_throws(string? password)
    {
        var act = () => _sut.RegisterAsync(new RegisterRequest("new@taskapp.com", password!));

        await act.Should().ThrowAsync<AppValidationException>().WithMessage("*Password*");
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_with_existing_email_throws_conflict_ignoring_case()
    {
        _users.Setup(u => u.GetByEmailAsync("taken@taskapp.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "taken@taskapp.com" });

        var act = () => _sut.RegisterAsync(new RegisterRequest("  Taken@TaskApp.com ", ValidPassword));

        await act.Should().ThrowAsync<ConflictException>();
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_stores_hashed_password_never_plain_text()
    {
        User? saved = null;
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u);
        _tokens.Setup(t => t.Generate(It.IsAny<User>())).Returns("jwt");

        var result = await _sut.RegisterAsync(new RegisterRequest("New@TaskApp.com", ValidPassword));

        saved.Should().NotBeNull();
        saved!.PasswordHash.Should().Be($"HASHED::{ValidPassword.Length}").And.NotContain(ValidPassword);
        saved.Email.Should().Be("new@taskapp.com");
        saved.Id.Should().NotBeEmpty();
        result.Should().Be(new AuthResponse("jwt", "new@taskapp.com"));
    }

    [Fact]
    public async Task Login_with_unknown_email_throws_generic_invalid_credentials()
    {
        var act = () => _sut.LoginAsync(new LoginRequest("nobody@taskapp.com", ValidPassword));

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task Login_with_wrong_password_throws_same_generic_invalid_credentials()
    {
        GivenUser("demo@taskapp.com", passwordMatches: false);

        var act = () => _sut.LoginAsync(new LoginRequest("demo@taskapp.com", "Wrong1234"));

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid credentials");
        _tokens.Verify(t => t.Generate(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_from_generator()
    {
        var user = GivenUser("demo@taskapp.com", passwordMatches: true);
        _tokens.Setup(t => t.Generate(user)).Returns("signed.jwt.token");

        var result = await _sut.LoginAsync(new LoginRequest(" Demo@TaskApp.com", "Demo@1234"));

        result.Should().Be(new AuthResponse("signed.jwt.token", "demo@taskapp.com"));
    }

    private User GivenUser(string email, bool passwordMatches)
    {
        var user = new User { Id = Guid.NewGuid(), Email = email, PasswordHash = "stored-hash" };
        _users.Setup(u => u.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "stored-hash")).Returns(passwordMatches);
        return user;
    }
}
