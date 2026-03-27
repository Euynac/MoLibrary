namespace Monica.Core.ExceptionHandling.Exceptions;

/// <summary>
/// Represents a business exception that is typically used outside <see cref="Monica.Tool.Results.Res"/>-based flows.
/// </summary>
/// TODO: Consider suppressing stack traces for pure business errors.
public class BusinessException(string? message) : Exception(message)
{
}
