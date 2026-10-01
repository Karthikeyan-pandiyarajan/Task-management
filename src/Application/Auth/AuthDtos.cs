namespace Application.Auth;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

// Deliberately has no PasswordHash or entity reference.
public sealed record AuthResponse(string Token, string Email);
