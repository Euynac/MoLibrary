namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Invocation execution error.
/// </summary>
public class AutoModelInvokerException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);
