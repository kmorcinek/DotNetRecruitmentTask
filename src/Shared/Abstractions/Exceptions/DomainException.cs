namespace Abstractions.Exceptions;

public class DomainException(string message)
    : AppException(message);