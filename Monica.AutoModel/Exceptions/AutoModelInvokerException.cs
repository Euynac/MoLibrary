namespace Monica.AutoModel.Exceptions;

/// <summary>
/// 调用执行错误
/// </summary>
public class AutoModelInvokerException(string displayMessage, string? technicalDetail = null)
    : AutoModelBaseException(displayMessage, technicalDetail);