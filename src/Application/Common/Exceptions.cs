namespace Application.Common;

// Each exception maps to exactly one HTTP status in the API's global exception handler.

/// <summary>400 Bad Request</summary>
public sealed class AppValidationException(string message) : Exception(message);

/// <summary>404 Not Found</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>409 Conflict</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>401 Unauthorized</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
