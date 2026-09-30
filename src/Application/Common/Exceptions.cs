namespace Application.Common;

public sealed class AppValidationException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);
