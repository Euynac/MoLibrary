using Monica.Tool.MoResponse;

namespace Monica.Core.ExceptionHandler;

/// <summary>
/// Represents a business exception that is typically used outside <see cref="Res"/>-based flows.
/// </summary>
/// TODO: Consider suppressing stack traces for pure business errors.
public class MoExceptionBusinessError(string? message) : Exception(message)
{
    
}
