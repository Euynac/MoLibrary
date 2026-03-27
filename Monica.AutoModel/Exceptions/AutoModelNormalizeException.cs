namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Expression or query normalization/parsing error.
/// </summary>
public class AutoModelNormalizeException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);

/// <summary>
/// Token expression generation error.
/// </summary>
public class AutoModelTokenExpGenException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);
