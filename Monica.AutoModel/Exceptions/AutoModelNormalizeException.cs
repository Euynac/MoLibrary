namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Exception thrown when an expression or query cannot be normalized or parsed.
/// </summary>
public class AutoModelNormalizeException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);

/// <summary>
/// Exception thrown when token expression generation fails.
/// </summary>
public class AutoModelTokenExpGenException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);
