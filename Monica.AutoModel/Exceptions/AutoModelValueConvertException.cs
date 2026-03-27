namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Value conversion error.
/// </summary>
public class AutoModelValueConvertException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);
