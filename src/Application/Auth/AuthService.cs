using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Common;
using Domain.Entities;

namespace Application.Auth;

public sealed partial class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenGenerator tokens)
{
    public const int MinPasswordLength = 8;

    // One message for both "no such user" and "wrong password" so the endpoint can't be used to enumerate accounts.
    private const string InvalidCredentials = "Invalid credentials";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (!EmailPattern().IsMatch(email))
            throw new AppValidationException("A valid email address is required.");
        ValidatePassword(request.Password);

        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password)
        };
        await users.AddAsync(user, ct);

        return new AuthResponse(tokens.Generate(user), user.Email);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), ct);
        if (user is null || !hasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            throw new UnauthorizedException(InvalidCredentials);

        return new AuthResponse(tokens.Generate(user), user.Email);
    }

    // Emails are compared case-insensitively by storing them normalized.
    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static void ValidatePassword(string? password)
    {
        if (password is null
            || password.Length < MinPasswordLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new AppValidationException(
                $"Password must be at least {MinPasswordLength} characters and contain a letter and a digit.");
        }
    }

    // Pragmatic check (local@domain.tld, no whitespace); real ownership is only proven by a confirmation email.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
