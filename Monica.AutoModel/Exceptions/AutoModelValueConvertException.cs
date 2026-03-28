namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Exception thrown when a field value cannot be converted.
/// </summary>
public class AutoModelValueConvertException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);
