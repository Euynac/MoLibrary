namespace Monica.AutoModel.Exceptions;

/// <summary>
/// Exception thrown when an AutoModel invocation fails.
/// </summary>
public class AutoModelInvokerException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);
