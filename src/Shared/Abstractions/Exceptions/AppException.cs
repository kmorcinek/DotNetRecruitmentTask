namespace Abstractions.Exceptions;

public abstract class AppException(string message)
    : Exception(message);
