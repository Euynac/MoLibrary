using Monica.Core.ExceptionHandling.Exceptions;

namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Base exception for AutoModel execution failures.
/// </summary>
public class AutoModelBaseException : DisplayMessageException
{
    /// <summary>
    /// Creates a new AutoModel exception.
    /// </summary>
    /// <param name="displayMessage">User-facing message displayed to clients.</param>
    /// <param name="technicalDetail">Optional technical detail for diagnostics.</param>
    public AutoModelBaseException(string displayMessage, string? technicalDetail = null)
        : base(displayMessage, technicalDetail)
    {
    }
}
